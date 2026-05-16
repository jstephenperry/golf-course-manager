using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Models;

namespace FairwayHq.Api.Tests;

public class MembersTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public MembersTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Seeded_members_are_returned()
    {
        var client = _factory.CreateClient();
        var list = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.NotNull(list);
        Assert.True(list!.Count >= 5);
        Assert.Contains(list, m => m.Email == "eleanor.park@example.com");
    }

    [Fact]
    public async Task Crud_member()
    {
        var client = _factory.CreateClient();

        var draft = new MemberDto(
            Id: "",
            FirstName: "Test",
            LastName: "User",
            Email: "test.user@example.com",
            Phone: "555-0000",
            Tier: "Full",
            Handicap: 12.3,
            JoinDate: "2024-01-01",
            Active: true,
            Balance: 0m
        );

        var created = await (await client.PostAsJsonAsync("/api/members", draft))
            .Content.ReadFromJsonAsync<MemberDto>();
        Assert.NotNull(created);
        Assert.False(string.IsNullOrEmpty(created!.Id));
        Assert.Equal("Test", created.FirstName);

        var updated = created with { Handicap = 9.9 };
        var resUpd = await client.PutAsJsonAsync($"/api/members/{created.Id}", updated);
        Assert.Equal(HttpStatusCode.OK, resUpd.StatusCode);
        var after = await resUpd.Content.ReadFromJsonAsync<MemberDto>();
        Assert.Equal(9.9, after!.Handicap, 3);

        var del = await client.DeleteAsync($"/api/members/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var afterList = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.DoesNotContain(afterList!, m => m.Id == created.Id);
    }
}
