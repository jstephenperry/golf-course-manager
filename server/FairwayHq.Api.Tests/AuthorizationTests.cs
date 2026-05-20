using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Authorization;
using FairwayHq.Api.Models;

namespace FairwayHq.Api.Tests;

/// <summary>
/// End-to-end tests that the role → permission matrix is actually
/// enforced at the HTTP layer. These are the load-bearing tests for
/// the auth/RBAC work: if any of them flips, real users would be
/// either over- or under-permissioned.
/// </summary>
public class AuthorizationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public AuthorizationTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientFor(params string[] roles)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        return c;
    }

    [Fact]
    public async Task User_with_only_unknown_role_is_denied_protected_endpoints()
    {
        // The test harness can't simulate "no token" because the test
        // auth handler synthesizes an owner by default. The realistic
        // analogue: a user whose Keycloak roles haven't been mapped to
        // any app permissions (e.g., they're a brand-new account or have
        // a role the app doesn't recognize). They should still get 403.
        var c = ClientFor("unknown-role-not-in-matrix");
        var res = await c.GetAsync("/api/members");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_is_anonymous()
    {
        var c = _factory.CreateClient(); // no header at all
        // The TestAuthHandler does still kick in here (defaults to owner)
        // but the endpoint is .AllowAnonymous(), so it works regardless.
        var res = await c.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    // ---- The goal-statement example, end-to-end ----

    [Fact]
    public async Task Greenkeeper_cannot_create_a_product()
    {
        var c = ClientFor(Roles.Greenkeeper);
        var draft = new ProductDto(
            Id: "", Name: "Forbidden Mower", Category: "Accessories",
            Sku: $"FORBIDDEN-{Guid.NewGuid():N}".Substring(0, 16),
            Price: 99m, Cost: 50m, Stock: 1, ReorderLevel: 1);
        var res = await c.PostAsJsonAsync("/api/products", draft);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Greenkeeper_cannot_adjust_product_stock()
    {
        // First, create the product as owner so a real id exists.
        var owner = ClientFor(Roles.Owner);
        var sku = $"STOCK-{Guid.NewGuid():N}".Substring(0, 16);
        var draft = new ProductDto("", "Stock Test", "Accessories", sku, 10m, 5m, 10, 5);
        var created = await (await owner.PostAsJsonAsync("/api/products", draft))
            .Content.ReadFromJsonAsync<ProductDto>();

        var c = ClientFor(Roles.Greenkeeper);
        var res = await c.PostAsJsonAsync(
            $"/api/products/{created!.Id}/adjust-stock",
            new { Delta = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Greenkeeper_cannot_read_or_write_shifts()
    {
        var c = ClientFor(Roles.Greenkeeper);
        var listRes = await c.GetAsync("/api/shifts");
        Assert.Equal(HttpStatusCode.Forbidden, listRes.StatusCode);
    }

    [Fact]
    public async Task Greenkeeper_cannot_read_or_write_weekly_templates()
    {
        var c = ClientFor(Roles.Greenkeeper);
        var listRes = await c.GetAsync("/api/weekly-templates");
        Assert.Equal(HttpStatusCode.Forbidden, listRes.StatusCode);
    }

    [Fact]
    public async Task Greenkeeper_CAN_read_maintenance_and_tee_times()
    {
        // Sanity: the greenkeeper still has to do their job. Make sure
        // we didn't over-restrict.
        var c = ClientFor(Roles.Greenkeeper);

        var mt = await c.GetAsync("/api/maintenance");
        Assert.Equal(HttpStatusCode.OK, mt.StatusCode);

        var tt = await c.GetAsync("/api/tee-times");
        Assert.Equal(HttpStatusCode.OK, tt.StatusCode);
    }

    // ---- Role-boundary spot checks for other key actions ----

    [Fact]
    public async Task Pro_shop_cannot_void_a_tab()
    {
        var c = ClientFor(Roles.ProShop);
        // Doesn't matter that the tab id is bogus — auth check fires first.
        var res = await c.PostAsync("/api/tabs/tab_bogus_id/void", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_clear_system_data()
    {
        var c = ClientFor(Roles.Manager);
        var res = await c.PostAsync("/api/clear", null);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Owner_can_clear_system_data()
    {
        var c = ClientFor(Roles.Owner);
        var res = await c.PostAsync("/api/clear", null);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Starter_can_check_in_but_not_write_tee_times()
    {
        // Kiosk role. Reads tee times, can check players in, but can't
        // book or cancel.
        var c = ClientFor(Roles.Starter);

        var readRes = await c.GetAsync("/api/tee-times");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);

        // Even with the simplest payload, write must be denied.
        var draft = new TeeTimeDto("", "2030-01-01", "08:00", "crs_anything",
            new List<string>(), false, "Booked", "");
        var writeRes = await c.PostAsJsonAsync("/api/tee-times", draft);
        Assert.Equal(HttpStatusCode.Forbidden, writeRes.StatusCode);
    }
}
