using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class TabsEndpoints
{
    private static string NewId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 1 + 12);

    private static List<string> ParseList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new(); }
        catch { return new List<string>(); }
    }

    private static IQueryable<PlayerTab> TabsWithChildren(AppDbContext db) =>
        db.Tabs.Include(t => t.Items).Include(t => t.Payments);

    public static void MapTabs(this IEndpointRouteBuilder app)
    {
        var tabs = app.MapGroup("/api/tabs").WithTags("Tabs");

        // A11: paginated list-view that projects to a summary DTO — no
        // eager-loaded Items/Payments. Fetch GET /api/tabs/{id} for the full
        // tab with children.
        tabs.MapGet("/", async (int? offset, int? limit, AppDbContext db) =>
        {
            var (skip, take) = CrudEndpoints.PageParams(offset, limit);
            var rows = await db.Tabs.AsNoTracking()
                .OrderByDescending(t => t.OpenedAt).ThenBy(t => t.Id)
                .Skip(skip).Take(take)
                .Select(t => new
                {
                    t.Id, t.OpenedAt, t.ClosedAt, t.Status,
                    t.MemberIdsJson, t.GuestsJson, t.TeeTimeId,
                    ItemCount = t.Items.Count,
                    PaymentCount = t.Payments.Count,
                    Subtotal = t.Items.Sum(i => i.UnitPrice * i.Quantity),
                    t.TipAmount, t.TaxRate, t.Notes
                })
                .ToListAsync();
            return rows.Select(t => new PlayerTabSummaryDto(
                t.Id, t.OpenedAt, t.ClosedAt, t.Status,
                ParseList(t.MemberIdsJson), ParseList(t.GuestsJson), t.TeeTimeId,
                t.ItemCount, t.PaymentCount, t.Subtotal,
                t.TipAmount, t.TaxRate, t.Notes));
        }).RequireAuthorization(Policy.For(Permissions.TabsRead));

        tabs.MapGet("/{id}", async (string id, AppDbContext db) =>
        {
            var t = await TabsWithChildren(db).AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return t is null ? Results.NotFound() : Results.Ok(t.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TabsRead));

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
        }).RequireAuthorization(Policy.For(Permissions.TabsWrite));

        // Update metadata (status, members, tip, tax, notes, etc.)
        tabs.MapPut("/{id}", async (string id, PlayerTabDto dto, AppDbContext db) =>
        {
            var entity = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return Results.NotFound();
            entity.ApplyMeta(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TabsWrite));

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
                    product.Version++; // A12
                }
            }
            foreach (var pay in entity.Payments.Where(p => p.Method == "Member Charge" && !string.IsNullOrEmpty(p.PayerMemberId)))
            {
                var m = await db.Members.FindAsync(pay.PayerMemberId!);
                if (m is not null)
                {
                    // Tab void: post a balancing Payment entry sourced
                    // back to the tab. The original Charge entry stays as
                    // historical truth — the void is conceptually a
                    // counter-payment, not a reversal.
                    var res = MemberAccountService.PostPayment(
                        db, m, pay.Amount, method: null,
                        sourceKind: "Tab", sourceId: pay.Id,
                        note: $"Tab #{entity.Id} void", nowUtc: DateTime.UtcNow);
                    if (res.Error is not null) // A17: don't swallow service errors
                    {
                        await tx.RollbackAsync();
                        return Results.BadRequest(new { error = res.Error });
                    }
                }
            }
            entity.Status = "Voided";
            entity.ClosedAt = DateTime.UtcNow.ToString("o");
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TabsVoid));

        // Settle: requires zero balance computed server-side
        tabs.MapPost("/{id}/settle", async (string id, AppDbContext db) =>
        {
            var entity = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return Results.NotFound();
            // A9: round tax + total to cents via the shared money helper and
            // compare on the rounded balance rather than a magic epsilon.
            var subtotal = Money.Round(entity.Items.Sum(i => i.UnitPrice * i.Quantity));
            var tax = Money.Round(subtotal * entity.TaxRate);
            var total = Money.Round(subtotal + tax + entity.TipAmount);
            var paid = Money.Round(entity.Payments.Sum(p => p.Amount));
            var balance = Money.Round(total - paid);
            if (Money.IsOwed(balance))
            {
                return Results.BadRequest(new { error = "balance_outstanding", balance });
            }
            entity.Status = "Settled";
            entity.ClosedAt = DateTime.UtcNow.ToString("o");
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TabsSettle));

        // Reopen settled tab. A12: a Voided tab is terminal — reopening it
        // would resurrect stock/charge reversals already applied at void
        // time. Reopening is a settlement-class action, so it requires
        // tabs:settle rather than the broader tabs:write.
        tabs.MapPost("/{id}/reopen", async (string id, AppDbContext db) =>
        {
            var entity = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
            if (entity is null) return Results.NotFound();
            if (entity.Status == "Voided")
                return Results.BadRequest(new { error = "cannot_reopen_voided" });
            entity.Status = "Open";
            entity.ClosedAt = null;
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TabsSettle));

        // ----- items -----
        // A12: stock decrement guarded by the product concurrency token +
        // retry so simultaneous adds across tabs don't lose stock updates.
        tabs.MapPost("/{id}/items", async (string id, CreateLineItemDto body, AppDbContext db) =>
        {
            if (body.Quantity < 1)
                return Results.BadRequest(new { error = "quantity_must_be_positive" });

            return await ConcurrencyRetry.ExecuteAsync(db, async () =>
            {
                var tab = await TabsWithChildren(db).FirstOrDefaultAsync(x => x.Id == id);
                if (tab is null) return Results.NotFound();
                if (tab.Status != "Open")
                    return Results.BadRequest(new { error = "tab_not_open" });

                var product = await db.Products.FindAsync(body.ProductId);
                if (product is null) return Results.BadRequest(new { error = "unknown_product" });

                await using var tx = await db.Database.BeginTransactionAsync();
                // Decrement (clamp at zero — server is source of truth)
                product.Stock = Math.Max(0, product.Stock - body.Quantity);
                product.Version++;

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
        }).RequireAuthorization(Policy.For(Permissions.TabsWrite));

        tabs.MapPut("/{id}/items/{itemId}/quantity", async (string id, string itemId, AdjustQuantityBody body, AppDbContext db) =>
            await ConcurrencyRetry.ExecuteAsync(db, async () =>
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
                    product.Version++; // A12
                }
                item.Quantity = body.Quantity;
                await db.SaveChangesAsync();
                await tx.CommitAsync();

                var updated = await TabsWithChildren(db).AsNoTracking()
                    .FirstAsync(x => x.Id == id);
                return Results.Ok(updated.ToDto());
            })
        ).RequireAuthorization(Policy.For(Permissions.TabsWrite));

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
                product.Version++; // A12
            }
            db.TabLineItems.Remove(item);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var updated = await TabsWithChildren(db).AsNoTracking()
                .FirstAsync(x => x.Id == id);
            return Results.Ok(updated.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TabsWrite));

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
            // Generate the payment id up front so the ledger entry can reference it.
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
                var charge = MemberAccountService.PostCharge(
                    db, m, body.Amount, category: "F&B",
                    sourceKind: "Tab", sourceId: payment.Id,
                    note: payment.Note, nowUtc: DateTime.UtcNow);
                if (charge.Error is not null) // A17
                {
                    await tx.RollbackAsync();
                    return Results.BadRequest(new { error = charge.Error });
                }
            }
            db.TabPayments.Add(payment);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var updated = await TabsWithChildren(db).AsNoTracking()
                .FirstAsync(x => x.Id == id);
            return Results.Ok(updated.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TabsPayment));

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
                {
                    var res = MemberAccountService.PostPayment(
                        db, m, pay.Amount, method: null,
                        sourceKind: "Tab", sourceId: pay.Id,
                        note: $"Tab #{tab.Id} payment removed", nowUtc: DateTime.UtcNow);
                    if (res.Error is not null) // A17
                    {
                        await tx.RollbackAsync();
                        return Results.BadRequest(new { error = res.Error });
                    }
                }
            }
            db.TabPayments.Remove(pay);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            var updated = await TabsWithChildren(db).AsNoTracking()
                .FirstAsync(x => x.Id == id);
            return Results.Ok(updated.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TabsPayment));
    }

    public record AdjustQuantityBody(int Quantity);
}
