using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Models;
using FairwayHq.Api.Tests.Helpers;

namespace FairwayHq.Api.Tests;

public class SnapshotTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public SnapshotTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Snapshot_round_trip_restores_state()
    {
        var client = _factory.CreateClient();
        await TestSeed.MinimalAsync(client);

        var snap = await client.GetFromJsonAsync<DataSnapshot>("/api/snapshot");
        Assert.NotNull(snap);
        // TestSeed provisions exactly: 5 members + 1 course + 3 products + 2 tee times.
        Assert.True(snap!.Members.Count >= 5);
        Assert.True(snap.Courses.Count >= 1);

        // Clear, then restore — counts should match
        await client.PostAsync("/api/clear", null);
        var afterClear = await client.GetFromJsonAsync<DataSnapshot>("/api/snapshot");
        Assert.Empty(afterClear!.Members);

        var restored = await client.PostAsJsonAsync("/api/snapshot/restore", snap);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);

        var afterRestore = await client.GetFromJsonAsync<DataSnapshot>("/api/snapshot");
        Assert.Equal(snap.Members.Count, afterRestore!.Members.Count);
        Assert.Equal(snap.Courses.Count, afterRestore.Courses.Count);
        Assert.Equal(snap.Tabs.Count, afterRestore.Tabs.Count);
        // Tab children preserved
        Assert.Equal(
            snap.Tabs.Sum(t => t.Items.Count),
            afterRestore.Tabs.Sum(t => t.Items.Count));
    }

    [Fact]
    public async Task Reset_is_now_a_pure_clear_with_empty_seed()
    {
        // After Phase B's seed strip, /api/reset clears + invokes the
        // no-op Seed.EnsureSeeded. The endpoint stays available as an
        // extension point but no longer populates demo data.
        var client = _factory.CreateClient();
        await TestSeed.MinimalAsync(client);

        await client.PostAsync("/api/reset", null);
        var afterReset = await client.GetFromJsonAsync<DataSnapshot>("/api/snapshot");
        Assert.Empty(afterReset!.Members);
        Assert.Empty(afterReset.Courses);
        Assert.Empty(afterReset.Products);
    }
}
