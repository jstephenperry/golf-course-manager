using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Models;

namespace FairwayHq.Api.Tests;

/// <summary>
/// HTTP-shaped tests for the ledger endpoints. Service-level branch
/// coverage lives in LedgerServiceTests; these focus on endpoint
/// validation, status codes, pagination, and member-status guards.
/// Each count-sensitive test mints its own member to keep the shared
/// in-memory factory DB uncorrupted across the suite.
/// </summary>
public class LedgerTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public LedgerTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Ledger_endpoint_returns_404_for_unknown_member()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/members/does_not_exist/ledger");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Manual_charge_posts_entry_and_increments_balance()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);

        var res = await client.PostAsJsonAsync(
            $"/api/members/{memberId}/charges",
            new CreateManualChargeDto(75m, "Dues", "April dues"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var entry = await res.Content.ReadFromJsonAsync<MemberLedgerEntryDto>();
        Assert.Equal("Charge", entry!.EntryType);
        Assert.Equal("Dues", entry.Category);
        Assert.Equal(75m, entry.Amount);
        Assert.Equal("Manual", entry.SourceKind);

        var member = await GetMember(client, memberId);
        Assert.Equal(75m, member.Balance);
        Assert.NotNull(member.OldestUnpaidChargeAt);
    }

    [Fact]
    public async Task Manual_charge_rejects_unknown_category()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);

        var res = await client.PostAsJsonAsync(
            $"/api/members/{memberId}/charges",
            new CreateManualChargeDto(10m, "Bogus", ""));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Manual_charge_rejects_synthetic_Payment_category()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);

        var res = await client.PostAsJsonAsync(
            $"/api/members/{memberId}/charges",
            new CreateManualChargeDto(10m, "Payment", ""));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Manual_charge_rejected_on_suspended_member()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);
        await client.PostAsJsonAsync(
            $"/api/members/{memberId}/suspend", new { reviewer = (string?)null, note = "test" });

        var res = await client.PostAsJsonAsync(
            $"/api/members/{memberId}/charges",
            new CreateManualChargeDto(10m, "Dues", ""));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Manual_payment_posts_entry_and_decrements_balance()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);

        await client.PostAsJsonAsync($"/api/members/{memberId}/charges",
            new CreateManualChargeDto(100m, "F&B", "tab balance"));
        var afterCharge = await GetMember(client, memberId);
        Assert.Equal(100m, afterCharge.Balance);

        var res = await client.PostAsJsonAsync(
            $"/api/members/{memberId}/payments",
            new CreateManualPaymentDto(60m, "Card", "partial"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var member = await GetMember(client, memberId);
        Assert.Equal(40m, member.Balance);
        Assert.NotNull(member.OldestUnpaidChargeAt); // still positive balance
    }

    [Fact]
    public async Task Manual_payment_rejects_unknown_method()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);
        await client.PostAsJsonAsync($"/api/members/{memberId}/charges",
            new CreateManualChargeDto(10m, "Dues", ""));

        var res = await client.PostAsJsonAsync(
            $"/api/members/{memberId}/payments",
            new CreateManualPaymentDto(10m, "Bitcoin", ""));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Manual_payment_allowed_on_suspended_member_and_reinstates_on_full_paydown()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);

        // Build past-due balance, then suspend.
        await client.PostAsJsonAsync($"/api/members/{memberId}/charges",
            new CreateManualChargeDto(100m, "Dues", "back dues"));
        await client.PostAsJsonAsync(
            $"/api/members/{memberId}/suspend", new { reviewer = (string?)null, note = "" });
        var suspended = await GetMember(client, memberId);
        Assert.Equal("Suspended", suspended.Status);

        // Pay it off — payment should be accepted (status guard lifted).
        var payRes = await client.PostAsJsonAsync(
            $"/api/members/{memberId}/payments",
            new CreateManualPaymentDto(100m, "Cash", "paid in full"));
        Assert.Equal(HttpStatusCode.Created, payRes.StatusCode);

        var after = await GetMember(client, memberId);
        Assert.Equal(0m, after.Balance);
        Assert.Equal("Active", after.Status);
        Assert.Null(after.SuspendedAt);
    }

    [Fact]
    public async Task Void_endpoint_creates_reversal_and_marks_original_voided()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);

        var charge = await PostJsonAsync<MemberLedgerEntryDto>(
            client, $"/api/members/{memberId}/charges",
            new CreateManualChargeDto(40m, "Adjustment", "to void"));

        var voidRes = await client.PostAsJsonAsync(
            $"/api/members/ledger/{charge.Id}/void",
            new VoidLedgerEntryDto("reversed in test"));
        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);
        var reversal = await voidRes.Content.ReadFromJsonAsync<MemberLedgerEntryDto>();
        Assert.Equal("Reversal", reversal!.EntryType);
        Assert.Equal(charge.Id, reversal.ReversesEntryId);

        var member = await GetMember(client, memberId);
        Assert.Equal(0m, member.Balance);
    }

    [Fact]
    public async Task Void_endpoint_rejects_tab_sourced_entry()
    {
        // Service-level guard; surface as 400 here too.
        var client = _factory.CreateClient();
        // We construct a tab-sourced entry directly via DbContext since
        // the public endpoints don't expose SourceKind.
        var memberId = await CreateMember(client);

        string entryId;
        using (var db = _factory.CreateDbContext())
        {
            var member = await db.Members.FindAsync(memberId);
            var tabSourced = new MemberLedgerEntry
            {
                Id = "led_tabsourced01",
                MemberId = memberId,
                EntryType = "Charge",
                Category = "F&B",
                Amount = 5m,
                Note = "tab",
                PostedAt = DateTime.UtcNow.ToString("o"),
                SourceKind = "Tab",
                SourceId = "pay_fake",
            };
            db.MemberLedgerEntries.Add(tabSourced);
            member!.Balance += 5m;
            await db.SaveChangesAsync();
            entryId = tabSourced.Id;
        }

        var res = await client.PostAsJsonAsync(
            $"/api/members/ledger/{entryId}/void",
            new VoidLedgerEntryDto(""));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Ledger_list_paginates_newest_first()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);

        // 5 charges; small delay between so PostedAt strings differ.
        for (var i = 0; i < 5; i++)
        {
            await client.PostAsJsonAsync($"/api/members/{memberId}/charges",
                new CreateManualChargeDto(10m, "Adjustment", $"#{i}"));
            await Task.Delay(2);
        }

        var page1 = await client.GetFromJsonAsync<MemberLedgerListDto>(
            $"/api/members/{memberId}/ledger?limit=2");
        Assert.Equal(2, page1!.Entries.Count);
        Assert.True(page1.HasMore);
        // Newest first by PostedAt
        Assert.True(string.Compare(
            page1.Entries[0].PostedAt, page1.Entries[1].PostedAt,
            StringComparison.Ordinal) > 0);

        var cursor = page1.Entries[^1].PostedAt;
        var page2 = await client.GetFromJsonAsync<MemberLedgerListDto>(
            $"/api/members/{memberId}/ledger?limit=2&before={Uri.EscapeDataString(cursor)}");
        Assert.Equal(2, page2!.Entries.Count);
        Assert.True(page2.HasMore);

        cursor = page2.Entries[^1].PostedAt;
        var page3 = await client.GetFromJsonAsync<MemberLedgerListDto>(
            $"/api/members/{memberId}/ledger?limit=2&before={Uri.EscapeDataString(cursor)}");
        Assert.Single(page3!.Entries);
        Assert.False(page3.HasMore);
    }

    [Fact]
    public async Task Ledger_cache_matches_compute_after_mixed_operations()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client);

        await client.PostAsJsonAsync($"/api/members/{memberId}/charges",
            new CreateManualChargeDto(60m, "Dues", "c1"));
        await client.PostAsJsonAsync($"/api/members/{memberId}/charges",
            new CreateManualChargeDto(40m, "F&B", "c2"));
        await client.PostAsJsonAsync($"/api/members/{memberId}/payments",
            new CreateManualPaymentDto(30m, "Cash", "p1"));

        var member = await GetMember(client, memberId);
        using var db = _factory.CreateDbContext();
        var computed = FairwayHq.Api.Services.MemberAccountService
            .ComputeOldestUnpaid(db, memberId);
        Assert.Equal(computed, member.OldestUnpaidChargeAt);
        Assert.True(member.Balance > 0m);
    }

    // ----- helpers -----

    private static async Task<string> CreateMember(HttpClient client)
    {
        var draft = new MemberDto(
            Id: "", FirstName: "Led", LastName: $"Test_{Guid.NewGuid():N}".Substring(0, 12),
            Email: $"led_{Guid.NewGuid():N}@example.com",
            Phone: "555", Tier: "Full", Handicap: 0,
            JoinDate: "2024-01-01",
            Active: true, Balance: 0m, Status: "Active",
            OldestUnpaidChargeAt: null, SuspendedAt: null, Notes: null);
        var res = await client.PostAsJsonAsync("/api/members", draft);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<MemberDto>();
        return created!.Id;
    }

    private static async Task<MemberDto> GetMember(HttpClient client, string id)
    {
        var list = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        return list!.Single(m => m.Id == id);
    }

    private static async Task<T> PostJsonAsync<T>(HttpClient client, string path, object body)
    {
        var res = await client.PostAsJsonAsync(path, body);
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<T>())!;
    }
}
