using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class OpsEndpoints
{
    // A13: cap on the total number of rows a single restore body may carry,
    // backstopping the Kestrel body-size limit with a semantic count limit.
    private const int MaxRestoreRows = 50_000;

    // A2: a member's recomputed-from-ledger balance must match the supplied
    // MemberDto.Balance to within a cent, else the snapshot is internally
    // inconsistent and we reject it.
    private const decimal BalanceTolerance = 0.01m;
    public static void MapOps(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status = "ok",
            time = DateTime.UtcNow.ToString("o")
        })).WithTags("Ops").AllowAnonymous();

        app.MapGet("/api/snapshot", async (AppDbContext db) =>
        {
            var snap = await BuildSnapshot(db);
            return Results.Ok(snap);
        }).WithTags("Ops").RequireAuthorization(Policy.For(Permissions.SnapshotExport));

        app.MapPost("/api/snapshot/restore", async (DataSnapshot body, AppDbContext db) =>
        {
            // A13: reject oversized snapshots before doing any work.
            var ledgerCount = body.LedgerEntries?.Count ?? 0;
            var totalRows = body.Members.Count + body.MemberApplications.Count
                + (body.Nines?.Count ?? 0) + body.Courses.Count + body.TeeTimes.Count
                + body.Staff.Count + body.Shifts.Count + body.WeeklyTemplates.Count
                + body.Products.Count + body.Tournaments.Count + body.Maintenance.Count
                + body.Tabs.Count + ledgerCount;
            if (totalRows > MaxRestoreRows)
                return Results.BadRequest(new { error = "payload_too_large", maxRows = MaxRestoreRows, count = totalRows });

            // A2: validate the snapshot reconciles BEFORE mutating the DB —
            // reconstruct each member's balance from the restored ledger and
            // reject the whole restore if the supplied MemberDto.Balance
            // disagrees. Restored Member.Balance is then sourced from the
            // ledger, not blindly trusted from the DTO.
            var ledgerByMember = (body.LedgerEntries ?? new List<MemberLedgerEntryDto>())
                .Select(d => new MemberLedgerEntry
                {
                    Id = d.Id, MemberId = d.MemberId, EntryType = d.EntryType,
                    Category = d.Category, Amount = d.Amount, Method = d.Method,
                    Note = d.Note, PostedAt = d.PostedAt, SourceKind = d.SourceKind,
                    SourceId = d.SourceId, ReversesEntryId = d.ReversesEntryId,
                    VoidedAt = d.VoidedAt, VoidedByEntryId = d.VoidedByEntryId,
                })
                .GroupBy(e => e.MemberId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var reconciledBalances = new Dictionary<string, decimal>(StringComparer.Ordinal);
            foreach (var d in body.Members)
            {
                var entries = ledgerByMember.TryGetValue(d.Id, out var es)
                    ? es : new List<MemberLedgerEntry>();
                var computed = MemberAccountService.ComputeBalanceFromLedger(entries);
                reconciledBalances[d.Id] = computed;
                if (Math.Abs(computed - d.Balance) > BalanceTolerance)
                {
                    return Results.BadRequest(new
                    {
                        error = "ledger_balance_mismatch",
                        memberId = d.Id,
                        suppliedBalance = d.Balance,
                        ledgerBalance = computed,
                    });
                }
            }

            await using var tx = await db.Database.BeginTransactionAsync();

            db.TabLineItems.RemoveRange(db.TabLineItems);
            db.TabPayments.RemoveRange(db.TabPayments);
            db.Tabs.RemoveRange(db.Tabs);
            db.MemberLedgerEntries.RemoveRange(db.MemberLedgerEntries);
            db.Maintenance.RemoveRange(db.Maintenance);
            db.Tournaments.RemoveRange(db.Tournaments);
            db.Products.RemoveRange(db.Products);
            db.WeeklyTemplates.RemoveRange(db.WeeklyTemplates);
            db.Shifts.RemoveRange(db.Shifts);
            db.Staff.RemoveRange(db.Staff);
            db.TeeTimes.RemoveRange(db.TeeTimes);
            db.Courses.RemoveRange(db.Courses);
            // Drop nine + nested children. Order matters: courses (above)
            // reference Nines via FrontNineId/BackNineId with Restrict,
            // so Courses must clear first. HoleYardages/Holes/TeeSets are
            // cascade-deleted with the parent Nine, but draining them
            // explicitly keeps the change-tracker shallow.
            db.HoleYardages.RemoveRange(db.HoleYardages);
            db.Holes.RemoveRange(db.Holes);
            db.NineTeeSets.RemoveRange(db.NineTeeSets);
            db.Nines.RemoveRange(db.Nines);
            db.MemberApplications.RemoveRange(db.MemberApplications);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();

            foreach (var d in body.Members)
            {
                var e = new Member { Id = d.Id };
                e.Apply(d);
                // A2: source the restored balance from the reconciled ledger,
                // not the DTO (which was only used as a consistency check).
                e.Balance = reconciledBalances.TryGetValue(d.Id, out var b) ? b : 0m;
                db.Members.Add(e);
            }
            foreach (var d in body.MemberApplications)
            {
                var e = new MemberApplication { Id = d.Id };
                e.Apply(d); db.MemberApplications.Add(e);
            }
            // Restore Nines (with nested tee sets, holes, yardages) before
            // Courses so the FrontNineId/BackNineId FKs resolve.
            foreach (var d in body.Nines ?? new List<NineDto>())
            {
                var n = new Nine
                {
                    Id = d.Id, Name = d.Name,
                    Description = d.Description, Notes = d.Notes
                };
                foreach (var t in d.TeeSets)
                {
                    n.TeeSets.Add(new NineTeeSet
                    {
                        Id = t.Id, NineId = d.Id, Name = t.Name,
                        Color = t.Color, SortOrder = t.SortOrder
                    });
                }
                foreach (var h in d.Holes)
                {
                    var hole = new Hole
                    {
                        Id = h.Id, NineId = d.Id, Number = h.Number,
                        Par = h.Par, HandicapIndex = h.HandicapIndex,
                        Notes = h.Notes
                    };
                    foreach (var y in h.Yardages)
                    {
                        hole.Yardages.Add(new HoleYardage
                        {
                            Id = y.Id, HoleId = h.Id,
                            TeeSetId = y.TeeSetId, Yards = y.Yards
                        });
                    }
                    n.Holes.Add(hole);
                }
                db.Nines.Add(n);
            }
            foreach (var d in body.Courses)
            {
                var e = new Course { Id = d.Id };
                e.Apply(d); db.Courses.Add(e);
            }
            foreach (var d in body.TeeTimes)
            {
                var e = new TeeTime { Id = d.Id };
                e.Apply(d); db.TeeTimes.Add(e);
            }
            foreach (var d in body.Staff)
            {
                var e = new StaffMember { Id = d.Id };
                e.Apply(d); db.Staff.Add(e);
            }
            foreach (var d in body.Shifts)
            {
                var e = new Shift { Id = d.Id };
                e.Apply(d); db.Shifts.Add(e);
            }
            foreach (var d in body.WeeklyTemplates)
            {
                var e = new WeeklyTemplate { Id = d.Id };
                e.Apply(d); db.WeeklyTemplates.Add(e);
            }
            foreach (var d in body.Products)
            {
                var e = new Product { Id = d.Id };
                e.Apply(d); db.Products.Add(e);
            }
            foreach (var d in body.Tournaments)
            {
                var e = new Tournament { Id = d.Id };
                e.Apply(d); db.Tournaments.Add(e);
            }
            foreach (var d in body.Maintenance)
            {
                var e = new MaintenanceTask { Id = d.Id };
                e.Apply(d); db.Maintenance.Add(e);
            }
            foreach (var d in body.Tabs)
            {
                var e = new PlayerTab { Id = d.Id };
                e.ApplyMeta(d);
                foreach (var li in d.Items)
                {
                    e.Items.Add(new TabLineItem
                    {
                        Id = li.Id, TabId = d.Id, ProductId = li.ProductId,
                        Name = li.Name, UnitPrice = li.UnitPrice,
                        Quantity = li.Quantity, Notes = li.Notes,
                        AddedAt = li.AddedAt
                    });
                }
                foreach (var p in d.Payments)
                {
                    e.Payments.Add(new TabPayment
                    {
                        Id = p.Id, TabId = d.Id, Method = p.Method,
                        Amount = p.Amount, PayerMemberId = p.PayerMemberId,
                        Note = p.Note, PaidAt = p.PaidAt
                    });
                }
                db.Tabs.Add(e);
            }
            // Ledger entries are append-only — no Apply() helper, just
            // reconstruct from DTOs. Nullable: pre-ledger backups omit the field.
            foreach (var d in body.LedgerEntries ?? new List<MemberLedgerEntryDto>())
            {
                db.MemberLedgerEntries.Add(new MemberLedgerEntry
                {
                    Id = d.Id,
                    MemberId = d.MemberId,
                    EntryType = d.EntryType,
                    Category = d.Category,
                    Amount = d.Amount,
                    Method = d.Method,
                    Note = d.Note,
                    PostedAt = d.PostedAt,
                    SourceKind = d.SourceKind,
                    SourceId = d.SourceId,
                    ReversesEntryId = d.ReversesEntryId,
                    VoidedAt = d.VoidedAt,
                    VoidedByEntryId = d.VoidedByEntryId
                });
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { restored = true });
        }).WithTags("Ops").RequireAuthorization(Policy.For(Permissions.SnapshotRestore));

        app.MapPost("/api/reset", async (AppDbContext db) =>
        {
            // Wipe + reseed
            await ClearAll(db);
            Seed.EnsureSeeded(db);
            return Results.Ok(new { reset = true });
        }).WithTags("Ops").RequireAuthorization(Policy.For(Permissions.SystemClear));

        app.MapPost("/api/clear", async (AppDbContext db) =>
        {
            await ClearAll(db);
            return Results.Ok(new { cleared = true });
        }).WithTags("Ops").RequireAuthorization(Policy.For(Permissions.SystemClear));
    }

    public static async Task<DataSnapshot> BuildSnapshot(AppDbContext db)
    {
        var tabs = await db.Tabs.Include(t => t.Items).Include(t => t.Payments)
            .AsNoTracking().ToListAsync();
        var nines = await db.Nines
            .Include(n => n.TeeSets)
            .Include(n => n.Holes).ThenInclude(h => h.Yardages)
            .AsNoTracking()
            .ToListAsync();
        return new DataSnapshot(
            (await db.Members.AsNoTracking().ToListAsync()).Select(m => m.ToDto()).ToList(),
            (await db.Courses.AsNoTracking().ToListAsync()).Select(c => c.ToDto()).ToList(),
            (await db.TeeTimes.AsNoTracking().ToListAsync()).Select(t => t.ToDto()).ToList(),
            (await db.Staff.AsNoTracking().ToListAsync()).Select(s => s.ToDto()).ToList(),
            (await db.Shifts.AsNoTracking().ToListAsync()).Select(s => s.ToDto()).ToList(),
            (await db.WeeklyTemplates.AsNoTracking().ToListAsync()).Select(t => t.ToDto()).ToList(),
            (await db.Products.AsNoTracking().ToListAsync()).Select(p => p.ToDto()).ToList(),
            (await db.Tournaments.AsNoTracking().ToListAsync()).Select(t => t.ToDto()).ToList(),
            (await db.Maintenance.AsNoTracking().ToListAsync()).Select(m => m.ToDto()).ToList(),
            tabs.Select(t => t.ToDto()).ToList(),
            (await db.MemberApplications.AsNoTracking().ToListAsync()).Select(a => a.ToDto()).ToList(),
            (await db.MemberLedgerEntries.AsNoTracking().ToListAsync()).Select(e => e.ToDto()).ToList(),
            nines.Select(n => n.ToDto()).ToList()
        );
    }

    private static async Task ClearAll(AppDbContext db)
    {
        db.TabLineItems.RemoveRange(db.TabLineItems);
        db.TabPayments.RemoveRange(db.TabPayments);
        db.Tabs.RemoveRange(db.Tabs);
        db.MemberLedgerEntries.RemoveRange(db.MemberLedgerEntries);
        db.Maintenance.RemoveRange(db.Maintenance);
        db.Tournaments.RemoveRange(db.Tournaments);
        db.Products.RemoveRange(db.Products);
        db.WeeklyTemplates.RemoveRange(db.WeeklyTemplates);
        db.Shifts.RemoveRange(db.Shifts);
        db.Staff.RemoveRange(db.Staff);
        db.TeeTimes.RemoveRange(db.TeeTimes);
        // Courses reference Nines with Restrict — drop courses first.
        db.Courses.RemoveRange(db.Courses);
        db.HoleYardages.RemoveRange(db.HoleYardages);
        db.Holes.RemoveRange(db.Holes);
        db.NineTeeSets.RemoveRange(db.NineTeeSets);
        db.Nines.RemoveRange(db.Nines);
        db.MemberApplications.RemoveRange(db.MemberApplications);
        db.Members.RemoveRange(db.Members);
        await db.SaveChangesAsync();
    }
}
