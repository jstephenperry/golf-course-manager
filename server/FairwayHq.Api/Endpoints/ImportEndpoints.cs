using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class ImportEndpoints
{
    private static string NewId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 1 + 12);

    public record ImportRowError(int Index, string? Id, string Error, string? Detail = null);
    public record ImportResult(int Created, int Skipped, List<ImportRowError> Errors);

    public static void MapImport(this IEndpointRouteBuilder app)
    {
        // ----- members
        app.MapPost("/api/import/members",
            async (List<MemberDto> rows, AppDbContext db) =>
                Results.Ok(await ImportMembers(db, rows))
        ).WithTags("Import");

        // ----- courses
        app.MapPost("/api/import/courses",
            async (List<CourseDto> rows, AppDbContext db) =>
                Results.Ok(await ImportCourses(db, rows))
        ).WithTags("Import");

        // ----- tee times (FK: CourseId)
        app.MapPost("/api/import/tee-times",
            async (List<TeeTimeDto> rows, AppDbContext db) =>
                Results.Ok(await ImportTeeTimes(db, rows))
        ).WithTags("Import");

        // ----- staff
        app.MapPost("/api/import/staff",
            async (List<StaffMemberDto> rows, AppDbContext db) =>
                Results.Ok(await ImportStaff(db, rows))
        ).WithTags("Import");

        // ----- shifts (FK: StaffId)
        app.MapPost("/api/import/shifts",
            async (List<ShiftDto> rows, AppDbContext db) =>
                Results.Ok(await ImportShifts(db, rows))
        ).WithTags("Import");

        // ----- weekly templates (FK: StaffId)
        app.MapPost("/api/import/weekly-templates",
            async (List<WeeklyTemplateDto> rows, AppDbContext db) =>
                Results.Ok(await ImportWeeklyTemplates(db, rows))
        ).WithTags("Import");

        // ----- products
        app.MapPost("/api/import/products",
            async (List<ProductDto> rows, AppDbContext db) =>
                Results.Ok(await ImportProducts(db, rows))
        ).WithTags("Import");

        // ----- tournaments (FK: CourseId)
        app.MapPost("/api/import/tournaments",
            async (List<TournamentDto> rows, AppDbContext db) =>
                Results.Ok(await ImportTournaments(db, rows))
        ).WithTags("Import");

        // ----- maintenance (FK: CourseId, AssignedTo)
        app.MapPost("/api/import/maintenance",
            async (List<MaintenanceTaskDto> rows, AppDbContext db) =>
                Results.Ok(await ImportMaintenance(db, rows))
        ).WithTags("Import");
    }

    private static async Task<ImportResult> ImportMembers(AppDbContext db, List<MemberDto> rows)
    {
        var existing = (await db.Members.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName))
            { errors.Add(new(i, r.Id, "required_field_missing", "FirstName and LastName required")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Member { Id = string.IsNullOrEmpty(r.Id) ? NewId("mbr") : r.Id };
            e.Apply(r);
            db.Members.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportCourses(AppDbContext db, List<CourseDto> rows)
    {
        var existing = (await db.Courses.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Name))
            { errors.Add(new(i, r.Id, "required_field_missing", "Name required")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Course { Id = string.IsNullOrEmpty(r.Id) ? NewId("crs") : r.Id };
            e.Apply(r);
            db.Courses.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportTeeTimes(AppDbContext db, List<TeeTimeDto> rows)
    {
        var existing = (await db.TeeTimes.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var courseIds = (await db.Courses.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Date) || string.IsNullOrWhiteSpace(r.Time))
            { errors.Add(new(i, r.Id, "required_field_missing", "Date and Time required")); continue; }
            if (!courseIds.Contains(r.CourseId))
            { errors.Add(new(i, r.Id, "fk_missing", $"CourseId {r.CourseId} not found")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new TeeTime { Id = string.IsNullOrEmpty(r.Id) ? NewId("tee") : r.Id };
            e.Apply(r);
            db.TeeTimes.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportStaff(AppDbContext db, List<StaffMemberDto> rows)
    {
        var existing = (await db.Staff.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName))
            { errors.Add(new(i, r.Id, "required_field_missing", "FirstName and LastName required")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new StaffMember { Id = string.IsNullOrEmpty(r.Id) ? NewId("stf") : r.Id };
            e.Apply(r);
            db.Staff.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportShifts(AppDbContext db, List<ShiftDto> rows)
    {
        var existing = (await db.Shifts.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var staffIds = (await db.Staff.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Date) || string.IsNullOrWhiteSpace(r.Start) || string.IsNullOrWhiteSpace(r.End))
            { errors.Add(new(i, r.Id, "required_field_missing", "Date, Start, End required")); continue; }
            if (!staffIds.Contains(r.StaffId))
            { errors.Add(new(i, r.Id, "fk_missing", $"StaffId {r.StaffId} not found")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Shift { Id = string.IsNullOrEmpty(r.Id) ? NewId("shft") : r.Id };
            e.Apply(r);
            db.Shifts.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportWeeklyTemplates(AppDbContext db, List<WeeklyTemplateDto> rows)
    {
        var existing = (await db.WeeklyTemplates.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var staffIds = (await db.Staff.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Start) || string.IsNullOrWhiteSpace(r.End))
            { errors.Add(new(i, r.Id, "required_field_missing", "Start and End required")); continue; }
            if (r.DayOfWeek < 0 || r.DayOfWeek > 6)
            { errors.Add(new(i, r.Id, "invalid_day_of_week", "DayOfWeek must be 0–6")); continue; }
            if (!staffIds.Contains(r.StaffId))
            { errors.Add(new(i, r.Id, "fk_missing", $"StaffId {r.StaffId} not found")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new WeeklyTemplate { Id = string.IsNullOrEmpty(r.Id) ? NewId("wtmp") : r.Id };
            e.Apply(r);
            db.WeeklyTemplates.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportProducts(AppDbContext db, List<ProductDto> rows)
    {
        var existing = (await db.Products.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Sku))
            { errors.Add(new(i, r.Id, "required_field_missing", "Name and Sku required")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Product { Id = string.IsNullOrEmpty(r.Id) ? NewId("prod") : r.Id };
            e.Apply(r);
            db.Products.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportTournaments(AppDbContext db, List<TournamentDto> rows)
    {
        var existing = (await db.Tournaments.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var courseIds = (await db.Courses.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Date))
            { errors.Add(new(i, r.Id, "required_field_missing", "Name and Date required")); continue; }
            if (!courseIds.Contains(r.CourseId))
            { errors.Add(new(i, r.Id, "fk_missing", $"CourseId {r.CourseId} not found")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Tournament { Id = string.IsNullOrEmpty(r.Id) ? NewId("trn") : r.Id };
            e.Apply(r);
            db.Tournaments.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportMaintenance(AppDbContext db, List<MaintenanceTaskDto> rows)
    {
        var existing = (await db.Maintenance.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var courseIds = (await db.Courses.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var staffIds = (await db.Staff.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Title))
            { errors.Add(new(i, r.Id, "required_field_missing", "Title required")); continue; }
            // CourseId and AssignedTo are optional; validate when present.
            if (!string.IsNullOrEmpty(r.CourseId) && !courseIds.Contains(r.CourseId))
            { errors.Add(new(i, r.Id, "fk_missing", $"CourseId {r.CourseId} not found")); continue; }
            if (!string.IsNullOrEmpty(r.AssignedTo) && !staffIds.Contains(r.AssignedTo))
            { errors.Add(new(i, r.Id, "fk_missing", $"AssignedTo {r.AssignedTo} not found")); continue; }
            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new MaintenanceTask { Id = string.IsNullOrEmpty(r.Id) ? NewId("mnt") : r.Id };
            e.Apply(r);
            db.Maintenance.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }
}
