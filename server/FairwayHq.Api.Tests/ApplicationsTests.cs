using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Models;

namespace FairwayHq.Api.Tests;

public class ApplicationsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public ApplicationsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Full_application_lifecycle_creates_member_with_initiation_fee()
    {
        var client = _factory.CreateClient();

        // 1. Submit
        var submitted = await (await client.PostAsJsonAsync("/api/applications", new
        {
            firstName = "Casey",
            lastName = "Lin",
            email = "casey.lin@example.com",
            phone = "555-1212",
            requestedTier = "Weekday",
            initiationFee = 750m,
            notes = ""
        })).Content.ReadFromJsonAsync<MemberApplicationDto>();
        Assert.Equal("Pending", submitted!.Status);

        // 2. Approve
        var approved = await (await client.PostAsJsonAsync(
            $"/api/applications/{submitted.Id}/approve",
            new { reviewer = "tester", note = "ok" }
        )).Content.ReadFromJsonAsync<MemberApplicationDto>();
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal("tester", approved.ReviewedBy);

        // 3. Activate — creates the member
        var activated = await (await client.PostAsync(
            $"/api/applications/{submitted.Id}/activate", null
        )).Content.ReadFromJsonAsync<ActivationResult>();
        Assert.Equal("Activated", activated!.Application.Status);
        Assert.False(string.IsNullOrEmpty(activated.Member.Id));
        Assert.Equal(activated.Member.Id, activated.Application.ActivatedMemberId);
        Assert.Equal("Active", activated.Member.Status);
        Assert.Equal(750m, activated.Member.Balance);
        Assert.False(string.IsNullOrEmpty(activated.Member.OldestUnpaidChargeAt),
            "Initiation fee should start the NET-X aging clock");

        // Member is now reachable via /api/members
        var members = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.Contains(members!, m => m.Id == activated.Member.Id);

        // The initiation fee posted a ledger entry tagged with the application id.
        using var db = _factory.CreateDbContext();
        var ledger = db.MemberLedgerEntries
            .Where(e => e.MemberId == activated.Member.Id)
            .ToList();
        var initiationEntry = Assert.Single(ledger);
        Assert.Equal("Charge", initiationEntry.EntryType);
        Assert.Equal("Initiation", initiationEntry.Category);
        Assert.Equal(750m, initiationEntry.Amount);
        Assert.Equal("Application", initiationEntry.SourceKind);
        Assert.Equal(submitted.Id, initiationEntry.SourceId);
    }

    [Fact]
    public async Task Reject_then_activate_is_forbidden()
    {
        var client = _factory.CreateClient();
        var submitted = await (await client.PostAsJsonAsync("/api/applications", new
        {
            firstName = "Reject",
            lastName = "Me",
            email = "x@example.com",
            phone = "",
            requestedTier = "Full",
            initiationFee = 0m,
            notes = ""
        })).Content.ReadFromJsonAsync<MemberApplicationDto>();

        await client.PostAsJsonAsync($"/api/applications/{submitted!.Id}/reject", new { reviewer = "t", note = "no" });

        var actRes = await client.PostAsync($"/api/applications/{submitted.Id}/activate", null);
        Assert.Equal(HttpStatusCode.BadRequest, actRes.StatusCode);
    }

    private record ActivationResult(MemberApplicationDto Application, MemberDto Member);
}
