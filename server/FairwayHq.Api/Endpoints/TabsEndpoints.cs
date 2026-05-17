using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class TabsEndpoints
{
    private static string NewId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 1 + 12);

    private static IQueryable<PlayerTab> TabsWithChildren(AppDbContext db) =>
        db.Tabs.Include(t => t.Items).Include(t => t.Payments);

    public static void MapTabs(this IEndpointRouteBuilder app)
    {
        var tabs = app.MapGroup("/api/tabs").WithTags("Tabs");

        tabs.MapGet("/", async (AppDbContext db) =>
            (await TabsWithChildren(db).AsNoTracking().ToListAsync())
            .Select(t => t.ToDto()));

        tabs.MapGet("/{id}", async (string id, AppDbContext db) =>
        {
            var t = await TabsWithChildren(db).AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return t is null ? Results.NotFound() : Results.Ok(t.ToDto());
        });

        // Open a tab (no children yet)
        tabs.MapPost("/", async (PlayerTabDto dto, AppDbContext db) =>
        {
            var entity = new PlayerTab
            {
                Id = string.IsNullOrEmpty(dto.Id) ? NewId("tab") : dto.Id
            };
            entity.ApplyMeta(dto);
            db.Tabs.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/tabs/{entity.Id}", entity.ToDto());
        });

        // Update metadata (status, members, tip, tax, notes, etc.)
        tabs.MapPut("/{id}", async (string id, PlayerTabDto dto, AppDbContext db) =>
        {
            var entity = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return Results.NotFound();
            entity.ApplyMeta(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        });

        // Void: restore stock, reverse member charges, mark Voided.
        tabs.MapPost("/{id}/void", async (string id, AppDbContext db) =>
        {
            var entity = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return Results.NotFound();
            if (entity.Status == "Voided") return Results.Ok(entity.ToDto());

            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var item in entity.Items)
            {
                var product = await db.Products.FindAsync(item.ProductId);
                if (product is not null)
                {
                    product.Stock += item.Quantity;
                }
            }
            foreach (var pay in entity.Payments.Where(p => p.Method == "Member Charge" && !string.IsNullOrEmpty(p.PayerMemberId)))
            {
                var m = await db.Members.FindAsync(pay.PayerMemberId!);
                if (m is not null)
                {
                    MemberAccountService.CreditMember(m, pay.Amount);
                }
            }
            entity.Status = "Voided";
            entity.ClosedAt = DateTime.UtcNow.ToString("o");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(entity.ToDto());
        });

        // Settle: requires zero balance computed server-side
        tabs.MapPost("/{id}/settle", async (string id, AppDbContext db) =>
        {
            var entity = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return Results.NotFound();
            var subtotal = entity.Items.Sum(i => i.UnitPrice * i.Quantity);
            var total = subtotal + (subtotal * entity.TaxRate) + entity.TipAmount;
            var paid = entity.Payments.Sum(p => p.Amount);
            var balance = total - paid;
            if (balance > 0.005m)
            {
                return Results.BadRequest(new { error = "balance_outstanding", balance });
            }
            entity.Status = "Settled";
            entity.ClosedAt = DateTime.UtcNow.ToString("o");
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        });

        // Reopen settled tab
        tabs.MapPost("/{id}/reopen", async (string id, AppDbContext db) =>
        {
            var entity = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return Results.NotFound();
            entity.Status = "Open";
            entity.ClosedAt = null;
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        });

        // ----- items -----
        tabs.MapPost("/{id}/items", async (string id, CreateLineItemDto body, AppDbContext db) =>
        {
            if (body.Quantity < 1)
                return Results.BadRequest(new { error = "quantity_must_be_positive" });

            var tab = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (tab is null) return Results.NotFound();
            if (tab.Status != "Open")
                return Results.BadRequest(new { error = "tab_not_open" });

            var product = await db.Products.FindAsync(body.ProductId);
            if (product is null) return Results.BadRequest(new { error = "unknown_product" });

            await using var tx = await db.Database.BeginTransactionAsync();
            // Decrement (clamp at zero — server is source of truth)
            product.Stock = Math.Max(0, product.Stock - body.Quantity);

            var item = new TabLineItem
            {
                Id = NewId("li"),
                TabId = tab.Id,
                ProductId = product.Id,
                Name = product.Name,
                UnitPrice = product.Price,
                Quantity = body.Quantity,
                Notes = body.Notes ?? string.Empty,
                AddedAt = DateTime.UtcNow.ToString("o")
            };
            db.TabLineItems.Add(item);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var updated = await TabsWithChildren(db).AsNoTracking()
                .FirstAsync(x => x.Id == id);
            return Results.Ok(updated.ToDto());
        });

        tabs.MapPut("/{id}/items/{itemId}/quantity", async (string id, string itemId, AdjustQuantityBody body, AppDbContext db) =>
        {
            var tab = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (tab is null) return Results.NotFound();
            if (tab.Status != "Open")
                return Results.BadRequest(new { error = "tab_not_open" });
            var item = tab.Items.FirstOrDefault(i => i.Id == itemId);
            if (item is null) return Results.NotFound();

            var delta = body.Quantity - item.Quantity;
            if (delta == 0)
                return Results.Ok(tab.ToDto());
            if (body.Quantity < 1)
                return Results.BadRequest(new { error = "quantity_must_be_positive" });

            await using var tx = await db.Database.BeginTransactionAsync();
            var product = await db.Products.FindAsync(item.ProductId);
            if (product is not null)
            {
                product.Stock = Math.Max(0, product.Stock - delta);
            }
            item.Quantity = body.Quantity;
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var updated = await TabsWithChildren(db).AsNoTracking()
                .FirstAsync(x => x.Id == id);
            return Results.Ok(updated.ToDto());
        });

        tabs.MapDelete("/{id}/items/{itemId}", async (string id, string itemId, AppDbContext db) =>
        {
            var tab = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (tab is null) return Results.NotFound();
            if (tab.Status != "Open")
                return Results.BadRequest(new { error = "tab_not_open" });
            var item = tab.Items.FirstOrDefault(i => i.Id == itemId);
            if (item is null) return Results.NotFound();

            await using var tx = await db.Database.BeginTransactionAsync();
            var product = await db.Products.FindAsync(item.ProductId);
            if (product is not null)
            {
                product.Stock += item.Quantity;
            }
            db.TabLineItems.Remove(item);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var updated = await TabsWithChildren(db).AsNoTracking()
                .FirstAsync(x => x.Id == id);
            return Results.Ok(updated.ToDto());
        });

        // ----- payments -----
        tabs.MapPost("/{id}/payments", async (string id, CreatePaymentDto body, AppDbContext db) =>
        {
            if (body.Amount <= 0)
                return Results.BadRequest(new { error = "amount_must_be_positive" });
            var tab = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (tab is null) return Results.NotFound();
            if (tab.Status != "Open")
                return Results.BadRequest(new { error = "tab_not_open" });

            await using var tx = await db.Database.BeginTransactionAsync();
            if (body.Method == "Member Charge")
            {
                if (string.IsNullOrEmpty(body.PayerMemberId))
                    return Results.BadRequest(new { error = "payer_required" });
                var m = await db.Members.FindAsync(body.PayerMemberId);
                if (m is null) return Results.BadRequest(new { error = "unknown_member" });
                if (m.Status == "Suspended")
                    return Results.BadRequest(new { error = "member_suspended", memberId = m.Id });
                if (m.Status == "Inactive")
                    return Results.BadRequest(new { error = "member_inactive", memberId = m.Id });
                MemberAccountService.ChargeMember(m, body.Amount, DateTime.UtcNow);
            }
            var payment = new TabPayment
            {
                Id = NewId("pay"),
                TabId = tab.Id,
                Method = body.Method,
                Amount = body.Amount,
                PayerMemberId = body.PayerMemberId,
                Note = body.Note ?? string.Empty,
                PaidAt = DateTime.UtcNow.ToString("o")
            };
            db.TabPayments.Add(payment);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var updated = await TabsWithChildren(db).AsNoTracking()
                .FirstAsync(x => x.Id == id);
            return Results.Ok(updated.ToDto());
        });

        tabs.MapDelete("/{id}/payments/{paymentId}", async (string id, string paymentId, AppDbContext db) =>
        {
            var tab = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (tab is null) return Results.NotFound();
            if (tab.Status != "Open")
                return Results.BadRequest(new { error = "tab_not_open" });
            var pay = tab.Payments.FirstOrDefault(p => p.Id == paymentId);
            if (pay is null) return Results.NotFound();

            await using var tx = await db.Database.BeginTransactionAsync();
            if (pay.Method == "Member Charge" && !string.IsNullOrEmpty(pay.PayerMemberId))
            {
                var m = await db.Members.FindAsync(pay.PayerMemberId);
                if (m is not null)
                    MemberAccountService.CreditMember(m, pay.Amount);
            }
            db.TabPayments.Remove(pay);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var updated = await TabsWithChildren(db).AsNoTracking()
                .FirstAsync(x => x.Id == id);
            return Results.Ok(updated.ToDto());
        });
    }

    public record AdjustQuantityBody(int Quantity);
}
