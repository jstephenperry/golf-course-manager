using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Models;

namespace FairwayHq.Api.Tests;

public class MemberOverviewTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public MemberOverviewTests(ApiFactory factory) => _factory = factory;

    private const string EleanorId = "mbr_J4nKp2vQ8x";
    private const string CourseId = "crs_K7nMpQjLxR";

    [Fact]
    public async Task Overview_returns_profile_for_seeded_member()
    {
        var client = _factory.CreateClient();
        var overview = await client.GetFromJsonAsync<MemberOverviewDto>(
            $"/api/members/{EleanorId}/overview");

        Assert.NotNull(overview);
        Assert.Equal(EleanorId, overview!.Member.Id);
        Assert.Equal("Eleanor", overview.Member.FirstName);
        Assert.Equal("Park", overview.Member.LastName);
        // Seed gives Eleanor at least 2 Completed rounds. Other tests in this
        // class don't add Completed rounds for Eleanor, so the count is stable.
        Assert.True(overview.LifetimeRounds >= 2);
        Assert.NotNull(overview.LastPlayedDate);
        Assert.All(overview.RecentRounds, r => Assert.Equal("Completed", r.Status));
        Assert.All(overview.RecentRounds, r => Assert.Contains(EleanorId, r.Players));
    }

    [Fact]
    public async Task Overview_recent_rounds_are_sorted_newest_first()
    {
        var client = _factory.CreateClient();
        var overview = await client.GetFromJsonAsync<MemberOverviewDto>(
            $"/api/members/{EleanorId}/overview");

        Assert.NotNull(overview);
        for (var i = 1; i < overview!.RecentRounds.Count; i++)
        {
            var prev = overview.RecentRounds[i - 1];
            var cur = overview.RecentRounds[i];
            var prevKey = $"{prev.Date}T{prev.Time}";
            var curKey = $"{cur.Date}T{cur.Time}";
            Assert.True(string.Compare(prevKey, curKey, StringComparison.Ordinal) > 0,
                $"Expected {prevKey} > {curKey}");
        }
    }

    [Fact]
    public async Task Overview_returns_zero_for_member_with_no_completed_rounds()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client, "NoRounds", "Member");

        var overview = await client.GetFromJsonAsync<MemberOverviewDto>(
            $"/api/members/{memberId}/overview");

        Assert.NotNull(overview);
        Assert.Equal(0, overview!.LifetimeRounds);
        Assert.Null(overview.LastPlayedDate);
        Assert.Empty(overview.RecentRounds);
    }

    [Fact]
    public async Task Overview_returns_404_for_unknown_member()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/members/does_not_exist/overview");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Overview_recent_rounds_capped_at_10_and_newest_first()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client, "Cap", "Tester");

        // 12 completed rounds across 12 distinct past dates.
        var today = DateTime.UtcNow.Date;
        for (var i = 12; i >= 1; i--)
        {
            var date = today.AddDays(-i - 30).ToString("yyyy-MM-dd");
            await CreateTeeTime(client, date, "08:00", memberId, "Completed");
        }

        var overview = await client.GetFromJsonAsync<MemberOverviewDto>(
            $"/api/members/{memberId}/overview");

        Assert.NotNull(overview);
        Assert.Equal(12, overview!.LifetimeRounds);
        Assert.Equal(10, overview.RecentRounds.Count);

        // Most-recent first.
        for (var i = 1; i < overview.RecentRounds.Count; i++)
        {
            var prev = overview.RecentRounds[i - 1];
            var cur = overview.RecentRounds[i];
            Assert.True(string.Compare(prev.Date, cur.Date, StringComparison.Ordinal) > 0,
                $"Expected {prev.Date} > {cur.Date}");
        }
    }

    [Fact]
    public async Task Overview_ignores_non_completed_rounds()
    {
        var client = _factory.CreateClient();
        var memberId = await CreateMember(client, "Filter", "Tester");

        var today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
        var future = DateTime.UtcNow.Date.AddDays(7).ToString("yyyy-MM-dd");
        var past = DateTime.UtcNow.Date.AddDays(-7).ToString("yyyy-MM-dd");

        await CreateTeeTime(client, future, "08:00", memberId, "Booked");
        await CreateTeeTime(client, today, "09:00", memberId, "Checked In");
        await CreateTeeTime(client, past, "10:00", memberId, "Completed");
        await CreateTeeTime(client, past, "11:00", memberId, "Cancelled");

        var overview = await client.GetFromJsonAsync<MemberOverviewDto>(
            $"/api/members/{memberId}/overview");

        Assert.NotNull(overview);
        Assert.Equal(1, overview!.LifetimeRounds);
        Assert.Equal(past, overview.LastPlayedDate);
        Assert.Single(overview.RecentRounds);
        Assert.Equal("Completed", overview.RecentRounds[0].Status);
    }

    private static async Task<string> CreateMember(HttpClient client, string first, string last)
    {
        var draft = new MemberDto(
            Id: "", FirstName: first, LastName: last,
            Email: $"{first}.{last}@test.example".ToLowerInvariant(),
            Phone: "555-0000", Tier: "Full",
            Handicap: 0, JoinDate: "2024-01-01",
            Active: true, Balance: 0m, Status: "Active",
            OldestUnpaidChargeAt: null, SuspendedAt: null, Notes: null
        );
        var res = await client.PostAsJsonAsync("/api/members", draft);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.Content.ReadFromJsonAsync<MemberDto>();
        return created!.Id;
    }

    private static async Task CreateTeeTime(HttpClient client, string date, string time,
        string memberId, string status)
    {
        var dto = new TeeTimeDto(
            Id: "", Date: date, Time: time, CourseId: CourseId,
            Players: new List<string> { memberId },
            Cart: false, Status: status, Notes: ""
        );
        var res = await client.PostAsJsonAsync("/api/tee-times", dto);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
    }
}
