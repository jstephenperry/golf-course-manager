using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FairwayHq.Api.Tests;

public class DunningTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public DunningTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Member_charge_stamps_oldest_unpaid_when_balance_was_zero()
    {
        var client = _factory.CreateClient();

        // Open a tab and post a Member Charge of $40 to m1 (starts at $0)
        var tab = await OpenTabFor(client, "mbr_J4nKp2vQ8x");
        await client.PostAsJsonAsync($"/api/tabs/{tab.Id}/payments", new
        {
            method = "Member Charge",
            amount = 40m,
            payerMemberId = "mbr_J4nKp2vQ8x",
            note = ""
        });

        var m = (await client.GetFromJsonAsync<List<MemberDto>>("/api/members"))!
            .Single(x => x.Id == "mbr_J4nKp2vQ8x");
        Assert.Equal(40m, m.Balance);
        Assert.False(string.IsNullOrEmpty(m.OldestUnpaidChargeAt));
    }

    [Fact]
    public async Task Dunning_run_suspends_member_past_NET60()
    {
        // Reach in via DI: stamp m1 with a 70-day-old unpaid charge, then trigger dunning.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = await db.Members.FindAsync("mbr_J4nKp2vQ8x");
        Assert.NotNull(m);
        m!.Balance = 125m;
        m.OldestUnpaidChargeAt = DateTime.UtcNow.AddDays(-70).ToString("o");
        m.Status = "Active";
        m.Active = true;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/dunning/run", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var result = await res.Content.ReadFromJsonAsync<DunningRunResultDto>();
        Assert.True(result!.Suspended >= 1);
        Assert.Contains("mbr_J4nKp2vQ8x", result.AffectedMemberIds);

        var refreshed = (await client.GetFromJsonAsync<List<MemberDto>>("/api/members"))!
            .Single(x => x.Id == "mbr_J4nKp2vQ8x");
        Assert.Equal("Suspended", refreshed.Status);
        Assert.False(refreshed.Active);
        Assert.False(string.IsNullOrEmpty(refreshed.SuspendedAt));
    }

    [Fact]
    public async Task Dunning_does_not_suspend_recent_charges()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = await db.Members.FindAsync("mbr_P3xYm5dBqV");
        m!.Balance = 50m;
        m.OldestUnpaidChargeAt = DateTime.UtcNow.AddDays(-10).ToString("o");
        m.Status = "Active";
        m.Active = true;
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        await client.PostAsync("/api/dunning/run", null);

        var refreshed = (await client.GetFromJsonAsync<List<MemberDto>>("/api/members"))!
            .Single(x => x.Id == "mbr_P3xYm5dBqV");
        Assert.Equal("Active", refreshed.Status);
    }

    [Fact]
    public async Task Paying_off_balance_auto_reinstates_a_suspended_member()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = await db.Members.FindAsync("mbr_F8jRn2KwHt");
        m!.Balance = 100m;
        m.Status = "Suspended";
        m.Active = false;
        m.SuspendedAt = DateTime.UtcNow.AddDays(-1).ToString("o");
        m.OldestUnpaidChargeAt = DateTime.UtcNow.AddDays(-70).ToString("o");
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        // Manual reinstate via endpoint requires balance to be zeroed first.
        // We do it by posting a Card payment that doesn't touch member balance —
        // instead, simulate the member paying their account directly via the
        // direct reinstate endpoint after we zero them through the API.
        // Easiest: call /reinstate after manually nulling balance via a member PUT.
        var put = await client.PutAsJsonAsync($"/api/members/mbr_F8jRn2KwHt", new
        {
            id = "mbr_F8jRn2KwHt",
            firstName = m.FirstName,
            lastName = m.LastName,
            email = m.Email,
            phone = m.Phone,
            tier = m.Tier,
            handicap = m.Handicap,
            joinDate = m.JoinDate,
            active = false,
            balance = 0m,
            status = "Suspended",
            oldestUnpaidChargeAt = (string?)null,
            suspendedAt = m.SuspendedAt
        });
        put.EnsureSuccessStatusCode();

        var res = await client.PostAsync("/api/dunning/run", null);
        var result = await res.Content.ReadFromJsonAsync<DunningRunResultDto>();
        Assert.True(result!.Reinstated >= 1);
        Assert.Contains("mbr_F8jRn2KwHt", result.AffectedMemberIds);

        var refreshed = (await client.GetFromJsonAsync<List<MemberDto>>("/api/members"))!
            .Single(x => x.Id == "mbr_F8jRn2KwHt");
        Assert.Equal("Active", refreshed.Status);
        Assert.True(refreshed.Active);
        Assert.Null(refreshed.SuspendedAt);
    }

    [Fact]
    public async Task Suspended_member_cannot_be_charged()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = await db.Members.FindAsync("mbr_W7gHk9rTfL");
        m!.Status = "Suspended";
        m.Active = false;
        m.SuspendedAt = DateTime.UtcNow.ToString("o");
        await db.SaveChangesAsync();

        var client = _factory.CreateClient();
        var tab = await OpenTabFor(client, "mbr_W7gHk9rTfL");
        var chargeRes = await client.PostAsJsonAsync($"/api/tabs/{tab.Id}/payments", new
        {
            method = "Member Charge",
            amount = 10m,
            payerMemberId = "mbr_W7gHk9rTfL",
            note = ""
        });
        Assert.Equal(HttpStatusCode.BadRequest, chargeRes.StatusCode);
    }

    [Fact]
    public async Task Dunning_suspends_via_real_ledger_entries_backdated_past_NET60()
    {
        // End-to-end: open tab → Member Charge (writes a ledger Charge
        // entry) → backdate both the ledger entry's PostedAt AND the
        // cached OldestUnpaidChargeAt to >60 days ago → run dunning →
        // assert suspended. Defends against any future refactor that
        // makes the cached field drift from ledger ground truth.
        var client = _factory.CreateClient();
        var tab = await OpenTabFor(client, "mbr_C6vDp9LqXz");
        // Priya is Inactive in seed — bring to Active for this test.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p = await db.Members.FindAsync("mbr_C6vDp9LqXz");
            p!.Status = "Active";
            p.Active = true;
            await db.SaveChangesAsync();
        }

        var chargeRes = await client.PostAsJsonAsync($"/api/tabs/{tab.Id}/payments", new
        {
            method = "Member Charge",
            amount = 80m,
            payerMemberId = "mbr_C6vDp9LqXz",
            note = "test"
        });
        chargeRes.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p = await db.Members.FindAsync("mbr_C6vDp9LqXz");
            var entry = db.MemberLedgerEntries
                .Where(e => e.MemberId == "mbr_C6vDp9LqXz" && e.SourceKind == "Tab")
                .OrderBy(e => e.PostedAt)
                .First();
            var backdated = DateTime.UtcNow.AddDays(-70).ToString("o");
            entry.PostedAt = backdated;
            p!.OldestUnpaidChargeAt = backdated;
            await db.SaveChangesAsync();
        }

        var run = await client.PostAsync("/api/dunning/run", null);
        var result = await run.Content.ReadFromJsonAsync<DunningRunResultDto>();
        Assert.Contains("mbr_C6vDp9LqXz", result!.AffectedMemberIds);

        var refreshed = (await client.GetFromJsonAsync<List<MemberDto>>("/api/members"))!
            .Single(x => x.Id == "mbr_C6vDp9LqXz");
        Assert.Equal("Suspended", refreshed.Status);
    }

    private static async Task<PlayerTabDto> OpenTabFor(HttpClient client, string memberId)
    {
        var res = await client.PostAsJsonAsync("/api/tabs", new
        {
            openedAt = DateTime.UtcNow.ToString("o"),
            status = "Open",
            memberIds = new[] { memberId },
            guests = Array.Empty<string>(),
            items = Array.Empty<object>(),
            payments = Array.Empty<object>(),
            tipAmount = 0m,
            taxRate = 0m,
            notes = ""
        });
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<PlayerTabDto>())!;
    }
}
