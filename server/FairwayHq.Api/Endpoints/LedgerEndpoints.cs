using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class LedgerEndpoints
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    public static void MapLedger(this IEndpointRouteBuilder app)
    {
        // List entries for a member, newest first, with cursor pagination.
        // `before` is the PostedAt of the last entry seen; the next page
        // returns entries strictly older than that. Id is used as a
        // tiebreaker for entries sharing the same millisecond-precision
        // ISO timestamp.
        app.MapGet("/api/members/{id}/ledger",
            async (string id, int? limit, string? before, AppDbContext db) =>
            {
                var member = await db.Members.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id);
                if (member is null) return Results.NotFound();

                var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

                var query = db.MemberLedgerEntries.AsNoTracking()
                    .Where(e => e.MemberId == id);
                if (!string.IsNullOrEmpty(before))
                {
                    // A10: ordinal comparison consistent with
                    // MemberAccountService's StringComparer.Ordinal ordering.
                    // EF Core can't translate string.CompareOrdinal /
                    // string.Compare(.., StringComparison), so we filter via
                    // the relational `<` operator which SQLite evaluates with
                    // the column's default BINARY (byte-ordinal) collation —
                    // the SQL-side equivalent of an ordinal comparison.
                    var cursor = before;
                    query = query.Where(e => string.Compare(e.PostedAt, cursor) < 0);
                }

                // Fetch one extra to detect HasMore without a second query.
                var rows = await query
                    .OrderByDescending(e => e.PostedAt)
                    .ThenByDescending(e => e.Id)
                    .Take(take + 1)
                    .ToListAsync();

                var hasMore = rows.Count > take;
                var page = rows.Take(take).Select(e => e.ToDto()).ToList();
                return Results.Ok(new MemberLedgerListDto(page, hasMore));
            }
        ).WithTags("Ledger").RequireAuthorization(Policy.For(Permissions.LedgerRead));

        app.MapPost("/api/members/{id}/charges",
            async (string id, CreateManualChargeDto body, AppDbContext db) =>
                // A6: retry on concurrency conflict against fresh member state.
                await ConcurrencyRetry.ExecuteAsync(db, async () =>
                {
                    var member = await db.Members.FindAsync(id);
                    if (member is null) return Results.NotFound();
                    // Suspended/Inactive members can't accrue new manual charges.
                    if (member.Status is "Suspended" or "Inactive")
                        return Results.BadRequest(new { error = "member_not_active" });

                    await using var tx = await db.Database.BeginTransactionAsync();
                    var result = MemberAccountService.PostCharge(
                        db, member, body.Amount, body.Category,
                        sourceKind: "Manual",
                        sourceId: null,
                        note: body.Note ?? string.Empty,
                        nowUtc: DateTime.UtcNow);
                    if (result.Error is not null)
                        return Results.BadRequest(new { error = result.Error });
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Results.Created(
                        $"/api/members/{id}/ledger/{result.Entry!.Id}",
                        result.Entry.ToDto());
                })
        ).WithTags("Ledger").RequireAuthorization(Policy.For(Permissions.LedgerCharge));

        app.MapPost("/api/members/{id}/payments",
            async (string id, CreateManualPaymentDto body, AppDbContext db) =>
                await ConcurrencyRetry.ExecuteAsync(db, async () =>
                {
                    var member = await db.Members.FindAsync(id);
                    if (member is null) return Results.NotFound();
                    // Payments allowed on any status — even Suspended members
                    // need a path to pay down their balance and auto-reinstate.

                    await using var tx = await db.Database.BeginTransactionAsync();
                    var result = MemberAccountService.PostPayment(
                        db, member, body.Amount, body.Method,
                        sourceKind: "Manual",
                        sourceId: null,
                        note: body.Note ?? string.Empty,
                        nowUtc: DateTime.UtcNow);
                    if (result.Error is not null)
                        return Results.BadRequest(new { error = result.Error });
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Results.Created(
                        $"/api/members/{id}/ledger/{result.Entry!.Id}",
                        result.Entry.ToDto());
                })
        ).WithTags("Ledger").RequireAuthorization(Policy.For(Permissions.LedgerPayment));

        app.MapPost("/api/members/ledger/{entryId}/void",
            async (string entryId, VoidLedgerEntryDto body, AppDbContext db) =>
                await ConcurrencyRetry.ExecuteAsync(db, async () =>
                {
                    var entry = await db.MemberLedgerEntries.FindAsync(entryId);
                    if (entry is null) return Results.NotFound();
                    var member = await db.Members.FindAsync(entry.MemberId);
                    if (member is null) return Results.NotFound();

                    await using var tx = await db.Database.BeginTransactionAsync();
                    var result = MemberAccountService.VoidEntry(
                        db, entry, member, body.Note ?? string.Empty, DateTime.UtcNow);
                    if (result.Error is not null)
                        return Results.BadRequest(new { error = result.Error });
                    await db.SaveChangesAsync();
                    await tx.CommitAsync();
                    return Results.Ok(result.Entry!.ToDto());
                })
        ).WithTags("Ledger").RequireAuthorization(Policy.For(Permissions.LedgerVoid));
    }
}
