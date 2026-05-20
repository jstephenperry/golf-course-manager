using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class CrudEndpoints
{
    private static string NewId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 1 + 12);

    public static void MapAll(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // ----- members
        var members = api.MapGroup("/members").WithTags("Members");
        members.MapGet("/", async (AppDbContext db) =>
            (await db.Members.AsNoTracking().ToListAsync()).Select(m => m.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.MembersRead));
        members.MapPost("/", async (MemberDto dto, AppDbContext db) =>
        {
            var entity = new Member { Id = string.IsNullOrEmpty(dto.Id) ? NewId("m") : dto.Id };
            entity.Apply(dto);
            db.Members.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/members/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersWrite));
        members.MapPut("/{id}", async (string id, MemberDto dto, AppDbContext db) =>
        {
            var entity = await db.Members.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersWrite));
        members.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.Members.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Members.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.MembersWrite));

        // ----- courses
        var courses = api.MapGroup("/courses").WithTags("Courses");
        courses.MapGet("/", async (AppDbContext db) =>
            (await db.Courses.AsNoTracking().ToListAsync()).Select(c => c.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.CoursesRead));
        courses.MapPost("/", async (CourseDto dto, AppDbContext db) =>
        {
            var entity = new Course { Id = string.IsNullOrEmpty(dto.Id) ? NewId("c") : dto.Id };
            entity.Apply(dto);
            db.Courses.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/courses/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.CoursesWrite));
        courses.MapPut("/{id}", async (string id, CourseDto dto, AppDbContext db) =>
        {
            var entity = await db.Courses.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.CoursesWrite));
        courses.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.Courses.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Courses.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.CoursesWrite));

        // ----- tee times
        var teeTimes = api.MapGroup("/tee-times").WithTags("TeeTimes");
        teeTimes.MapGet("/", async (AppDbContext db) =>
            (await db.TeeTimes.AsNoTracking().ToListAsync()).Select(t => t.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.TeeTimesRead));
        teeTimes.MapPost("/", async (TeeTimeDto dto, AppDbContext db) =>
        {
            var entity = new TeeTime { Id = string.IsNullOrEmpty(dto.Id) ? NewId("tt") : dto.Id };
            entity.Apply(dto);
            db.TeeTimes.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/tee-times/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TeeTimesWrite));
        teeTimes.MapPut("/{id}", async (string id, TeeTimeDto dto, AppDbContext db) =>
        {
            var entity = await db.TeeTimes.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
            // NOTE: status transitions (Checked In, Cancelled) ride on this
            // single write permission. Per ADR 0003, a finer-grained check
            // ("you can change status to Cancelled only with tee-times:cancel")
            // can be added later via an in-handler IAuthorizationService probe.
        }).RequireAuthorization(Policy.For(Permissions.TeeTimesWrite));
        teeTimes.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.TeeTimes.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.TeeTimes.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.TeeTimesCancel));

        // ----- staff
        var staff = api.MapGroup("/staff").WithTags("Staff");
        staff.MapGet("/", async (AppDbContext db) =>
            (await db.Staff.AsNoTracking().ToListAsync()).Select(s => s.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.StaffRead));
        staff.MapPost("/", async (StaffMemberDto dto, AppDbContext db) =>
        {
            var entity = new StaffMember { Id = string.IsNullOrEmpty(dto.Id) ? NewId("s") : dto.Id };
            entity.Apply(dto);
            db.Staff.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/staff/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.StaffWrite));
        staff.MapPut("/{id}", async (string id, StaffMemberDto dto, AppDbContext db) =>
        {
            var entity = await db.Staff.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.StaffWrite));
        staff.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.Staff.FindAsync(id);
            if (entity is null) return Results.NotFound();
            // cascade: shifts + templates referencing this staff
            var shifts = db.Shifts.Where(s => s.StaffId == id);
            var tpls = db.WeeklyTemplates.Where(t => t.StaffId == id);
            db.Shifts.RemoveRange(shifts);
            db.WeeklyTemplates.RemoveRange(tpls);
            db.Staff.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.StaffWrite));

        // ----- shifts
        var shifts2 = api.MapGroup("/shifts").WithTags("Shifts");
        shifts2.MapGet("/", async (AppDbContext db) =>
            (await db.Shifts.AsNoTracking().ToListAsync()).Select(s => s.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.ShiftsRead));
        shifts2.MapPost("/", async (ShiftDto dto, AppDbContext db) =>
        {
            var entity = new Shift { Id = string.IsNullOrEmpty(dto.Id) ? NewId("sh") : dto.Id };
            entity.Apply(dto);
            db.Shifts.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/shifts/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.ShiftsWrite));
        shifts2.MapPut("/{id}", async (string id, ShiftDto dto, AppDbContext db) =>
        {
            var entity = await db.Shifts.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.ShiftsWrite));
        shifts2.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.Shifts.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Shifts.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.ShiftsWrite));

        // ----- weekly templates
        var templates = api.MapGroup("/weekly-templates").WithTags("WeeklyTemplates");
        templates.MapGet("/", async (AppDbContext db) =>
            (await db.WeeklyTemplates.AsNoTracking().ToListAsync()).Select(t => t.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.TemplatesRead));
        templates.MapPost("/", async (WeeklyTemplateDto dto, AppDbContext db) =>
        {
            var entity = new WeeklyTemplate { Id = string.IsNullOrEmpty(dto.Id) ? NewId("wt") : dto.Id };
            entity.Apply(dto);
            db.WeeklyTemplates.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/weekly-templates/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TemplatesWrite));
        templates.MapPut("/{id}", async (string id, WeeklyTemplateDto dto, AppDbContext db) =>
        {
            var entity = await db.WeeklyTemplates.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TemplatesWrite));
        templates.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.WeeklyTemplates.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.WeeklyTemplates.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.TemplatesWrite));

        // ----- products (stock-adjust endpoint helps the client avoid races)
        var products = api.MapGroup("/products").WithTags("Products");
        products.MapGet("/", async (AppDbContext db) =>
            (await db.Products.AsNoTracking().ToListAsync()).Select(p => p.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.ProductsRead));
        products.MapPost("/", async (ProductDto dto, AppDbContext db) =>
        {
            var entity = new Product { Id = string.IsNullOrEmpty(dto.Id) ? NewId("p") : dto.Id };
            entity.Apply(dto);
            db.Products.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/products/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.ProductsWrite));
        products.MapPut("/{id}", async (string id, ProductDto dto, AppDbContext db) =>
        {
            var entity = await db.Products.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.ProductsWrite));
        products.MapPost("/{id}/adjust-stock", async (string id, [FromBody] AdjustStockBody body, AppDbContext db) =>
        {
            var entity = await db.Products.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Stock = Math.Max(0, entity.Stock + body.Delta);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.ProductsStock));
        products.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.Products.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Products.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.ProductsWrite));

        // ----- tournaments
        var tournaments = api.MapGroup("/tournaments").WithTags("Tournaments");
        tournaments.MapGet("/", async (AppDbContext db) =>
            (await db.Tournaments.AsNoTracking().ToListAsync()).Select(t => t.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.TournamentsRead));
        tournaments.MapPost("/", async (TournamentDto dto, AppDbContext db) =>
        {
            var entity = new Tournament { Id = string.IsNullOrEmpty(dto.Id) ? NewId("tr") : dto.Id };
            entity.Apply(dto);
            db.Tournaments.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/tournaments/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TournamentsWrite));
        tournaments.MapPut("/{id}", async (string id, TournamentDto dto, AppDbContext db) =>
        {
            var entity = await db.Tournaments.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.TournamentsWrite));
        tournaments.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.Tournaments.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Tournaments.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.TournamentsWrite));

        // ----- maintenance
        var maint = api.MapGroup("/maintenance").WithTags("Maintenance");
        maint.MapGet("/", async (AppDbContext db) =>
            (await db.Maintenance.AsNoTracking().ToListAsync()).Select(m => m.ToDto()))
            .RequireAuthorization(Policy.For(Permissions.MaintenanceRead));
        maint.MapPost("/", async (MaintenanceTaskDto dto, AppDbContext db) =>
        {
            var entity = new MaintenanceTask { Id = string.IsNullOrEmpty(dto.Id) ? NewId("mt") : dto.Id };
            entity.Apply(dto);
            db.Maintenance.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/maintenance/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MaintenanceWrite));
        maint.MapPut("/{id}", async (string id, MaintenanceTaskDto dto, AppDbContext db) =>
        {
            var entity = await db.Maintenance.FindAsync(id);
            if (entity is null) return Results.NotFound();
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MaintenanceWrite));
        maint.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.Maintenance.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.Maintenance.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.MaintenanceWrite));
    }

    public record AdjustStockBody(int Delta);
}
