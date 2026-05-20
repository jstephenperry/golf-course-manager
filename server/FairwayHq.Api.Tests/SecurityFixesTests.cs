using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FairwayHq.Api.Tests;

/// <summary>
/// Tests for the audit-finding fixes: A1 (auth fail-closed), A3 (member PUT
/// can't change balance/status), A6 (concurrent balance writes), A9 (server
/// money rounding), A14 (input validation).
/// </summary>
public class SecurityFixesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public SecurityFixesTests(ApiFactory factory) => _factory = factory;

    // ---------- A1: AuthSetup fails closed on Production + empty Authority ----------

    private sealed class FakeEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static IConfiguration ConfigWith(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void AuthSetup_throws_in_production_when_authority_is_empty()
    {
        var env = new FakeEnv { EnvironmentName = "Production" };
        var config = ConfigWith(new()
        {
            ["Authentication:Keycloak:Authority"] = "",
            ["Authentication:Keycloak:Audience"] = "fairway-hq-spa",
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => AuthSetup.ValidateKeycloakConfig(config, env));
        Assert.Contains("Authority", ex.Message);
    }

    [Fact]
    public void AuthSetup_throws_when_audience_is_empty()
    {
        var env = new FakeEnv { EnvironmentName = "Production" };
        var config = ConfigWith(new()
        {
            ["Authentication:Keycloak:Authority"] = "https://idp.example.com/realms/fairway",
            ["Authentication:Keycloak:Audience"] = "",
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => AuthSetup.ValidateKeycloakConfig(config, env));
        Assert.Contains("Audience", ex.Message);
    }

    [Fact]
    public void AuthSetup_accepts_production_config_with_authority_and_audience()
    {
        var env = new FakeEnv { EnvironmentName = "Production" };
        var config = ConfigWith(new()
        {
            ["Authentication:Keycloak:Authority"] = "https://idp.example.com/realms/fairway",
            ["Authentication:Keycloak:Audience"] = "fairway-hq-spa",
        });

        var (authority, _, audience, _) = AuthSetup.ValidateKeycloakConfig(config, env);
        Assert.Equal("https://idp.example.com/realms/fairway", authority);
        Assert.Equal("fairway-hq-spa", audience);
    }

    [Fact]
    public void AuthSetup_allows_empty_authority_outside_production()
    {
        var env = new FakeEnv { EnvironmentName = "Development" };
        var config = ConfigWith(new()
        {
            ["Authentication:Keycloak:Authority"] = "",
        });

        // Dev with no authority is allowed (audience defaults), no throw.
        var (authority, _, audience, _) = AuthSetup.ValidateKeycloakConfig(config, env);
        Assert.True(string.IsNullOrEmpty(authority));
        Assert.Equal("fairway-hq-spa", audience);
    }

    // ---------- A3: member PUT cannot change Balance/Status ----------

    [Fact]
    public async Task Member_put_cannot_change_balance_or_status()
    {
        var client = _factory.CreateClient();
        await TestSeed.MinimalAsync(client);

        // Give Eleanor a real balance + suspended status through the ledger /
        // service path (the only legitimate way now).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var m = await db.Members.FindAsync(TestSeed.EleanorId);
            m!.Balance = 75m;
            m.Status = "Suspended";
            m.Active = false;
            await db.SaveChangesAsync();
        }

        // Attempt mass-assignment via PUT: try to zero balance + flip status.
        var put = await client.PutAsJsonAsync($"/api/members/{TestSeed.EleanorId}", new
        {
            firstName = "Eleanor",
            lastName = "Park-Renamed",
            email = "eleanor.park@example.com",
            phone = "555-0142",
            tier = "Full",
            handicap = 8.4,
            joinDate = "2019-03-12",
            notes = "edited",
            // These should all be ignored by the profile-only update DTO:
            balance = 0m,
            status = "Active",
            active = true,
            oldestUnpaidChargeAt = (string?)null,
            suspendedAt = (string?)null,
        });
        put.EnsureSuccessStatusCode();

        var after = (await client.GetFromJsonAsync<List<MemberDto>>("/api/members"))!
            .Single(m => m.Id == TestSeed.EleanorId);

        // Profile fields updated; balance + status untouched.
        Assert.Equal("Park-Renamed", after.LastName);
        Assert.Equal("edited", after.Notes);
        Assert.Equal(75m, after.Balance);
        Assert.Equal("Suspended", after.Status);
        Assert.False(after.Active);
    }

    // ---------- A6: concurrent balance writes don't lose updates ----------

    [Fact]
    public async Task Stale_balance_write_is_detected_by_concurrency_token()
    {
        // A6: directly exercise the concurrency token. Two contexts load the
        // same member; the first write wins and bumps Version; the second
        // (now stale) write must raise DbUpdateConcurrencyException rather
        // than silently clobbering the winner's update.
        var client = _factory.CreateClient();
        await TestSeed.MinimalAsync(client);

        using var scopeA = _factory.Services.CreateScope();
        using var scopeB = _factory.Services.CreateScope();
        var dbA = scopeA.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbB = scopeB.ServiceProvider.GetRequiredService<AppDbContext>();

        var a = await dbA.Members.FindAsync(TestSeed.EleanorId);
        var b = await dbB.Members.FindAsync(TestSeed.EleanorId);

        a!.Balance += 10m; a.Version++;
        await dbA.SaveChangesAsync(); // winner

        b!.Balance += 25m; b.Version++; // stale: based on the pre-A version
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => dbB.SaveChangesAsync());
    }

    [Fact]
    public async Task Sequential_balance_writes_do_not_lose_updates()
    {
        // A6: with the retry/token path, a series of balance mutations all
        // land — the cached balance equals the ledger ground truth and no
        // charge is lost. Uses a dedicated member so the shared class
        // fixture's accumulated state from other tests doesn't interfere.
        var client = _factory.CreateClient();
        var created = await (await client.PostAsJsonAsync("/api/members", new MemberDto(
            Id: "", FirstName: "Concur", LastName: "Rency",
            Email: "concur.rency@example.com", Phone: "", Tier: "Full",
            Handicap: 0, JoinDate: "2024-01-01", Active: true, Balance: 0m,
            Status: "Active", OldestUnpaidChargeAt: null, SuspendedAt: null, Notes: null)))
            .Content.ReadFromJsonAsync<MemberDto>();
        var id = created!.Id;

        const int n = 8;
        const decimal each = 10m;
        for (var i = 0; i < n; i++)
        {
            var r = await client.PostAsJsonAsync(
                $"/api/members/{id}/charges", new
                {
                    amount = each,
                    category = "Adjustment",
                    note = "seq",
                });
            r.EnsureSuccessStatusCode();
        }

        var after = (await client.GetFromJsonAsync<List<MemberDto>>("/api/members"))!
            .Single(m => m.Id == id);
        Assert.Equal(n * each, after.Balance);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entries = await db.MemberLedgerEntries
            .Where(e => e.MemberId == id)
            .ToListAsync();
        Assert.Equal(n, entries.Count);
        Assert.Equal(n * each, entries.Sum(e => e.Amount));
    }

    // ---------- A9: server rounds tax/total without client pre-rounding ----------

    [Fact]
    public async Task Server_rounds_tax_and_total_without_client_prerounding()
    {
        var client = _factory.CreateClient();
        await TestSeed.MinimalAsync(client);

        // Open a tab with an awkward tax rate.
        var opened = await (await client.PostAsJsonAsync("/api/tabs", new
        {
            openedAt = DateTime.UtcNow.ToString("o"),
            status = "Open",
            memberIds = new[] { TestSeed.EleanorId },
            guests = Array.Empty<string>(),
            items = Array.Empty<object>(),
            payments = Array.Empty<object>(),
            tipAmount = 0m,
            taxRate = 0.0825m,
            notes = "",
        })).Content.ReadFromJsonAsync<PlayerTabDto>();

        // 3 iced teas @ 3.50 = 10.50 subtotal; tax = 0.866250 → rounds to 0.87;
        // total = 11.37.
        await client.PostAsJsonAsync($"/api/tabs/{opened!.Id}/items",
            new CreateLineItemDto(TestSeed.IcedTeaProductId, 3, ""));

        // Pay the server-rounded total of 11.37 — settle must succeed even
        // though the client did NOT pre-round the math.
        await client.PostAsJsonAsync($"/api/tabs/{opened.Id}/payments", new
        {
            method = "Card",
            amount = 11.37m,
            note = "",
        });

        var settle = await client.PostAsync($"/api/tabs/{opened.Id}/settle", null);
        Assert.Equal(HttpStatusCode.OK, settle.StatusCode);

        // A payment one cent short must NOT settle (proves we compare on the
        // rounded balance, not a loose epsilon).
        var opened2 = await (await client.PostAsJsonAsync("/api/tabs", new
        {
            openedAt = DateTime.UtcNow.ToString("o"),
            status = "Open",
            memberIds = new[] { TestSeed.EleanorId },
            guests = Array.Empty<string>(),
            items = Array.Empty<object>(),
            payments = Array.Empty<object>(),
            tipAmount = 0m,
            taxRate = 0.0825m,
            notes = "",
        })).Content.ReadFromJsonAsync<PlayerTabDto>();
        await client.PostAsJsonAsync($"/api/tabs/{opened2!.Id}/items",
            new CreateLineItemDto(TestSeed.IcedTeaProductId, 3, ""));
        await client.PostAsJsonAsync($"/api/tabs/{opened2.Id}/payments", new
        {
            method = "Card",
            amount = 11.36m,
            note = "",
        });
        var settleShort = await client.PostAsync($"/api/tabs/{opened2.Id}/settle", null);
        Assert.Equal(HttpStatusCode.BadRequest, settleShort.StatusCode);
    }

    // ---------- A14: negative/invalid CRUD payloads are rejected ----------

    [Fact]
    public async Task Negative_product_price_is_rejected()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/products", new ProductDto(
            Id: "", Name: "Bad", Category: "Accessories", Sku: "BAD-1",
            Price: -5m, Cost: 1m, Stock: 1, ReorderLevel: 0));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Unknown_member_tier_is_rejected()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/members", new MemberDto(
            Id: "", FirstName: "Test", LastName: "User",
            Email: "t@example.com", Phone: "", Tier: "Platinum-Unknown",
            Handicap: 1, JoinDate: "2024-01-01", Active: true, Balance: 0m,
            Status: "Active", OldestUnpaidChargeAt: null, SuspendedAt: null, Notes: null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Negative_member_balance_on_create_is_rejected()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/members", new MemberDto(
            Id: "", FirstName: "Test", LastName: "User",
            Email: "t2@example.com", Phone: "", Tier: "Full",
            Handicap: 1, JoinDate: "2024-01-01", Active: true, Balance: -50m,
            Status: "Active", OldestUnpaidChargeAt: null, SuspendedAt: null, Notes: null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Oversized_initiation_fee_is_rejected()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsJsonAsync("/api/applications", new MemberApplicationDto(
            Id: "", FirstName: "Big", LastName: "Spender",
            Email: "big@example.com", Phone: "", RequestedTier: "Full",
            SponsoringMemberId: null, InitiationFee: 9_999_999m, Notes: "",
            Status: "Pending", SubmittedAt: "", ReviewedAt: null,
            ReviewedBy: null, ReviewNote: null, ActivatedMemberId: null));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- A2: restore rejects ledger/balance-inconsistent snapshots ----------

    [Fact]
    public async Task Restore_rejects_snapshot_where_balance_disagrees_with_ledger()
    {
        var client = _factory.CreateClient();
        await TestSeed.MinimalAsync(client);

        var snap = (await client.GetFromJsonAsync<DataSnapshot>("/api/snapshot"))!;

        // Tamper: claim a member owes $500 with no backing ledger entries.
        var members = snap.Members.ToList();
        var idx = members.FindIndex(m => m.Id == TestSeed.EleanorId);
        members[idx] = members[idx] with { Balance = 500m };
        var tampered = snap with { Members = members };

        var res = await client.PostAsJsonAsync("/api/snapshot/restore", tampered);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    // ---------- A4: reviewer identity is server-stamped from the principal ----------

    [Fact]
    public async Task Approve_stamps_reviewer_from_principal_not_body()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, "alice.manager");

        var created = await (await client.PostAsJsonAsync("/api/applications", new MemberApplicationDto(
            Id: "", FirstName: "New", LastName: "Applicant",
            Email: "new@example.com", Phone: "", RequestedTier: "Full",
            SponsoringMemberId: null, InitiationFee: 0m, Notes: "",
            Status: "Pending", SubmittedAt: "", ReviewedAt: null,
            ReviewedBy: null, ReviewNote: null, ActivatedMemberId: null)))
            .Content.ReadFromJsonAsync<MemberApplicationDto>();

        // Try to spoof the reviewer via the body — must be ignored.
        var approved = await (await client.PostAsJsonAsync(
            $"/api/applications/{created!.Id}/approve",
            new { reviewer = "spoofed-attacker", note = "ok" }))
            .Content.ReadFromJsonAsync<MemberApplicationDto>();

        Assert.Equal("alice.manager", approved!.ReviewedBy);
        Assert.NotEqual("spoofed-attacker", approved.ReviewedBy);
    }
}
