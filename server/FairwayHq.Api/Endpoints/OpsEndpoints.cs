using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class OpsEndpoints
{
    public static void MapOps(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/health", () => Results.Ok(new
        {
            status = "ok",
            time = DateTime.UtcNow.ToString("o")
        })).WithTags("Ops");

        app.MapGet("/api/snapshot", async (AppDbContext db) =>
        {
            var snap = await BuildSnapshot(db);
            return Results.Ok(snap);
        }).WithTags("Ops");

        app.MapPost("/api/snapshot/restore", async (DataSnapshot body, AppDbContext db) =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();

            db.TabLineItems.RemoveRange(db.TabLineItems);
            db.TabPayments.RemoveRange(db.TabPayments);
            db.Tabs.RemoveRange(db.Tabs);
            db.Maintenance.RemoveRange(db.Maintenance);
            db.Tournaments.RemoveRange(db.Tournaments);
            db.Products.RemoveRange(db.Products);
            db.WeeklyTemplates.RemoveRange(db.WeeklyTemplates);
            db.Shifts.RemoveRange(db.Shifts);
            db.Staff.RemoveRange(db.Staff);
            db.TeeTimes.RemoveRange(db.TeeTimes);
            db.Courses.RemoveRange(db.Courses);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();

            foreach (var d in body.Members)
            {
                var e = new Member { Id = d.Id };
                e.Apply(d); db.Members.Add(e);
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
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Results.Ok(new { restored = true });
        }).WithTags("Ops");

        app.MapPost("/api/reset", async (AppDbContext db) =>
        {
            // Wipe + reseed
            db.TabLineItems.RemoveRange(db.TabLineItems);
            db.TabPayments.RemoveRange(db.TabPayments);
            db.Tabs.RemoveRange(db.Tabs);
            db.Maintenance.RemoveRange(db.Maintenance);
            db.Tournaments.RemoveRange(db.Tournaments);
            db.Products.RemoveRange(db.Products);
            db.WeeklyTemplates.RemoveRange(db.WeeklyTemplates);
            db.Shifts.RemoveRange(db.Shifts);
            db.Staff.RemoveRange(db.Staff);
            db.TeeTimes.RemoveRange(db.TeeTimes);
            db.Courses.RemoveRange(db.Courses);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();
            Seed.EnsureSeeded(db);
            return Results.Ok(new { reset = true });
        }).WithTags("Ops");

        app.MapPost("/api/clear", async (AppDbContext db) =>
        {
            db.TabLineItems.RemoveRange(db.TabLineItems);
            db.TabPayments.RemoveRange(db.TabPayments);
            db.Tabs.RemoveRange(db.Tabs);
            db.Maintenance.RemoveRange(db.Maintenance);
            db.Tournaments.RemoveRange(db.Tournaments);
            db.Products.RemoveRange(db.Products);
            db.WeeklyTemplates.RemoveRange(db.WeeklyTemplates);
            db.Shifts.RemoveRange(db.Shifts);
            db.Staff.RemoveRange(db.Staff);
            db.TeeTimes.RemoveRange(db.TeeTimes);
            db.Courses.RemoveRange(db.Courses);
            db.Members.RemoveRange(db.Members);
            await db.SaveChangesAsync();
            return Results.Ok(new { cleared = true });
        }).WithTags("Ops");
    }

    public static async Task<DataSnapshot> BuildSnapshot(AppDbContext db)
    {
        var tabs = await db.Tabs.Include(t => t.Items).Include(t => t.Payments)
            .AsNoTracking().ToListAsync();
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
            tabs.Select(t => t.ToDto()).ToList()
        );
    }
}
