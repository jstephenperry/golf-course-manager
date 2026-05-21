using System.Net;
using System.Net.Http.Json;

namespace FairwayHq.Api.Tests;

public class HealthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public HealthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadFromJsonAsync<HealthDto>();
        Assert.NotNull(body);
        Assert.Equal("ok", body!.Status);
        Assert.False(string.IsNullOrEmpty(body.Time));
    }

    private record HealthDto(string Status, string Time);
}
