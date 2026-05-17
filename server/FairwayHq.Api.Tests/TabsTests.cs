using System.Net;
using System.Net.Http.Json;
using FairwayHq.Api.Models;

namespace FairwayHq.Api.Tests;

public class TabsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public TabsTests(ApiFactory factory) => _factory = factory;

    private HttpClient ClientWithSeed() => _factory.CreateClient();

    [Fact]
    public async Task Open_settle_flow_decrements_stock_and_settles_at_zero_balance()
    {
        var client = ClientWithSeed();

        // Pick the iced tea product (stock 96 from seed, price 3.50)
        var products = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        var product = Assert.Single(products!.Where(p => p.Id == "prod_W6tRd3JmPk"));
        var startingStock = product.Stock;

        // Open a tab for member m1
        var opened = await (await client.PostAsJsonAsync("/api/tabs", new
        {
            openedAt = DateTime.UtcNow.ToString("o"),
            status = "Open",
            memberIds = new[] { "mbr_J4nKp2vQ8x" },
            guests = Array.Empty<string>(),
            items = Array.Empty<object>(),
            payments = Array.Empty<object>(),
            tipAmount = 0m,
            taxRate = 0.0825m,
            notes = ""
        })).Content.ReadFromJsonAsync<PlayerTabDto>();
        Assert.NotNull(opened);
        Assert.Equal("Open", opened!.Status);

        // Add 3 iced teas
        var afterAdd = await (await client.PostAsJsonAsync(
            $"/api/tabs/{opened.Id}/items",
            new CreateLineItemDto("prod_W6tRd3JmPk", 3, "")
        )).Content.ReadFromJsonAsync<PlayerTabDto>();
        Assert.Single(afterAdd!.Items);
        Assert.Equal(3, afterAdd.Items[0].Quantity);
        Assert.Equal(3.50m, afterAdd.Items[0].UnitPrice);

        // Stock decremented
        var afterProducts = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        var afterProduct = afterProducts!.Single(p => p.Id == "prod_W6tRd3JmPk");
        Assert.Equal(startingStock - 3, afterProduct.Stock);

        // Subtotal = 10.50, tax = 10.50 * 0.0825 = 0.866..., total ~11.37
        // Can't settle yet (balance > 0)
        var earlySettle = await client.PostAsync($"/api/tabs/{opened.Id}/settle", null);
        Assert.Equal(HttpStatusCode.BadRequest, earlySettle.StatusCode);

        // Pay the exact total via card
        var subtotal = 10.50m;
        var tax = subtotal * 0.0825m;
        var total = Math.Round(subtotal + tax, 2);
        await client.PostAsJsonAsync($"/api/tabs/{opened.Id}/payments", new
        {
            method = "Card",
            amount = total,
            note = ""
        });

        var settled = await client.PostAsync($"/api/tabs/{opened.Id}/settle", null);
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);
        var settledDto = await settled.Content.ReadFromJsonAsync<PlayerTabDto>();
        Assert.Equal("Settled", settledDto!.Status);
        Assert.False(string.IsNullOrEmpty(settledDto.ClosedAt));
    }

    [Fact]
    public async Task Voiding_restores_stock_and_reverses_member_charges()
    {
        var client = _factory.CreateClient();

        var products = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        var ball = products!.Single(p => p.Id == "prod_K2nM8wQjLp"); // Pro V1 dozen, $54.99
        var startingStock = ball.Stock;

        var members = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        var member = members!.Single(m => m.Id == "mbr_W7gHk9rTfL"); // Marcus, has balance 125.50
        var startingBalance = member.Balance;

        // Open tab for Marcus and add 2 dozen balls
        var opened = await (await client.PostAsJsonAsync("/api/tabs", new
        {
            openedAt = DateTime.UtcNow.ToString("o"),
            status = "Open",
            memberIds = new[] { "mbr_W7gHk9rTfL" },
            guests = Array.Empty<string>(),
            items = Array.Empty<object>(),
            payments = Array.Empty<object>(),
            tipAmount = 0m,
            taxRate = 0m,
            notes = ""
        })).Content.ReadFromJsonAsync<PlayerTabDto>();

        await client.PostAsJsonAsync($"/api/tabs/{opened!.Id}/items",
            new CreateLineItemDto("prod_K2nM8wQjLp", 2, ""));

        // Charge $50 to member's account
        await client.PostAsJsonAsync($"/api/tabs/{opened.Id}/payments", new
        {
            method = "Member Charge",
            amount = 50m,
            payerMemberId = "mbr_W7gHk9rTfL",
            note = ""
        });

        // Sanity: stock dropped, member balance went up
        var midProducts = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        Assert.Equal(startingStock - 2, midProducts!.Single(p => p.Id == "prod_K2nM8wQjLp").Stock);
        var midMembers = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.Equal(startingBalance + 50m, midMembers!.Single(m => m.Id == "mbr_W7gHk9rTfL").Balance);

        // Void the tab
        var voidRes = await client.PostAsync($"/api/tabs/{opened.Id}/void", null);
        Assert.Equal(HttpStatusCode.OK, voidRes.StatusCode);
        var voided = await voidRes.Content.ReadFromJsonAsync<PlayerTabDto>();
        Assert.Equal("Voided", voided!.Status);

        // Stock back, balance back
        var endProducts = await client.GetFromJsonAsync<List<ProductDto>>("/api/products");
        Assert.Equal(startingStock, endProducts!.Single(p => p.Id == "prod_K2nM8wQjLp").Stock);
        var endMembers = await client.GetFromJsonAsync<List<MemberDto>>("/api/members");
        Assert.Equal(startingBalance, endMembers!.Single(m => m.Id == "mbr_W7gHk9rTfL").Balance);

        // Ledger trail: one Charge entry from the Member Charge payment
        // (sourced by the TabPayment.Id) and one Payment entry from the
        // void (also sourced by the same TabPayment.Id).
        using var db = _factory.CreateDbContext();
        var entries = db.MemberLedgerEntries
            .Where(e => e.MemberId == "mbr_W7gHk9rTfL" && e.SourceKind == "Tab")
            .OrderBy(e => e.PostedAt)
            .ToList();
        Assert.Equal(2, entries.Count);
        Assert.Equal("Charge", entries[0].EntryType);
        Assert.Equal("F&B", entries[0].Category);
        Assert.Equal(50m, entries[0].Amount);
        Assert.Equal("Payment", entries[1].EntryType);
        Assert.Equal(50m, entries[1].Amount);
        // Both share the same TabPayment.Id as SourceId.
        Assert.Equal(entries[0].SourceId, entries[1].SourceId);
    }

    [Fact]
    public async Task Cannot_modify_settled_tab()
    {
        var client = _factory.CreateClient();

        // Open + settle a freebie tab (no items, no balance)
        var opened = await (await client.PostAsJsonAsync("/api/tabs", new
        {
            openedAt = DateTime.UtcNow.ToString("o"),
            status = "Open",
            memberIds = new[] { "mbr_J4nKp2vQ8x" },
            guests = Array.Empty<string>(),
            items = Array.Empty<object>(),
            payments = Array.Empty<object>(),
            tipAmount = 0m,
            taxRate = 0m,
            notes = ""
        })).Content.ReadFromJsonAsync<PlayerTabDto>();

        var settled = await client.PostAsync($"/api/tabs/{opened!.Id}/settle", null);
        Assert.Equal(HttpStatusCode.OK, settled.StatusCode);

        // Now try to add an item — should fail
        var addRes = await client.PostAsJsonAsync($"/api/tabs/{opened.Id}/items",
            new CreateLineItemDto("prod_W6tRd3JmPk", 1, ""));
        Assert.Equal(HttpStatusCode.BadRequest, addRes.StatusCode);
    }

    [Fact]
    public async Task Item_quantity_adjustment_rebalances_stock()
    {
        var client = _factory.CreateClient();
        var startingStock = (await client.GetFromJsonAsync<List<ProductDto>>("/api/products"))!
            .Single(p => p.Id == "prod_F4hLn7QxBg").Stock;

        var opened = await (await client.PostAsJsonAsync("/api/tabs", new
        {
            openedAt = DateTime.UtcNow.ToString("o"),
            status = "Open",
            memberIds = new[] { "mbr_J4nKp2vQ8x" },
            guests = Array.Empty<string>(),
            items = Array.Empty<object>(),
            payments = Array.Empty<object>(),
            tipAmount = 0m,
            taxRate = 0m,
            notes = ""
        })).Content.ReadFromJsonAsync<PlayerTabDto>();

        var afterAdd = await (await client.PostAsJsonAsync($"/api/tabs/{opened!.Id}/items",
            new CreateLineItemDto("prod_F4hLn7QxBg", 2, ""))).Content.ReadFromJsonAsync<PlayerTabDto>();
        var itemId = afterAdd!.Items[0].Id;

        // Bump to 5 (delta +3)
        await client.PutAsJsonAsync($"/api/tabs/{opened.Id}/items/{itemId}/quantity",
            new { quantity = 5 });

        var afterBump = (await client.GetFromJsonAsync<List<ProductDto>>("/api/products"))!
            .Single(p => p.Id == "prod_F4hLn7QxBg");
        Assert.Equal(startingStock - 5, afterBump.Stock);

        // Reduce to 1 (delta -4)
        await client.PutAsJsonAsync($"/api/tabs/{opened.Id}/items/{itemId}/quantity",
            new { quantity = 1 });
        var afterReduce = (await client.GetFromJsonAsync<List<ProductDto>>("/api/products"))!
            .Single(p => p.Id == "prod_F4hLn7QxBg");
        Assert.Equal(startingStock - 1, afterReduce.Stock);

        // Remove item entirely
        await client.DeleteAsync($"/api/tabs/{opened.Id}/items/{itemId}");
        var afterRemove = (await client.GetFromJsonAsync<List<ProductDto>>("/api/products"))!
            .Single(p => p.Id == "prod_F4hLn7QxBg");
        Assert.Equal(startingStock, afterRemove.Stock);
    }
}
