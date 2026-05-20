using FairwayHq.Api.Authorization;
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
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- nines (owns nested tee sets + holes + per-tee yardages)
        app.MapPost("/api/import/nines",
            async (List<NineDto> rows, AppDbContext db) =>
                Results.Ok(await ImportNines(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- courses (FK: FrontNineId, BackNineId — both optional)
        app.MapPost("/api/import/courses",
            async (List<CourseDto> rows, AppDbContext db) =>
                Results.Ok(await ImportCourses(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- tee times (FK: CourseId)
        app.MapPost("/api/import/tee-times",
            async (List<TeeTimeDto> rows, AppDbContext db) =>
                Results.Ok(await ImportTeeTimes(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- staff
        app.MapPost("/api/import/staff",
            async (List<StaffMemberDto> rows, AppDbContext db) =>
                Results.Ok(await ImportStaff(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- shifts (FK: StaffId)
        app.MapPost("/api/import/shifts",
            async (List<ShiftDto> rows, AppDbContext db) =>
                Results.Ok(await ImportShifts(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- weekly templates (FK: StaffId)
        app.MapPost("/api/import/weekly-templates",
            async (List<WeeklyTemplateDto> rows, AppDbContext db) =>
                Results.Ok(await ImportWeeklyTemplates(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- products
        app.MapPost("/api/import/products",
            async (List<ProductDto> rows, AppDbContext db) =>
                Results.Ok(await ImportProducts(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- tournaments (FK: CourseId)
        app.MapPost("/api/import/tournaments",
            async (List<TournamentDto> rows, AppDbContext db) =>
                Results.Ok(await ImportTournaments(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));

        // ----- maintenance (FK: CourseId, AssignedTo)
        app.MapPost("/api/import/maintenance",
            async (List<MaintenanceTaskDto> rows, AppDbContext db) =>
                Results.Ok(await ImportMaintenance(db, rows))
        ).WithTags("Import").RequireAuthorization(Policy.For(Permissions.ImportRun));
    }

    private static async Task<ImportResult> ImportMembers(AppDbContext db, List<MemberDto> rows)
    {
        var existing = (await db.Members.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        // Natural key: email (case-insensitive). Skip rows with no email
        // — two unidentified members can legitimately coexist.
        var byEmail = (await db.Members.AsNoTracking()
                .Where(m => m.Email != "")
                .Select(m => new { m.Id, m.Email })
                .ToListAsync())
            .ToDictionary(m => m.Email.Trim().ToLowerInvariant(), m => m.Id);

        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName))
            { errors.Add(new(i, r.Id, "required_field_missing", "FirstName and LastName required")); continue; }

            var emailKey = (r.Email ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(emailKey)
                && byEmail.TryGetValue(emailKey, out var existingByEmail)
                && existingByEmail != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"email '{r.Email}' already used by {existingByEmail}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Member { Id = string.IsNullOrEmpty(r.Id) ? NewId("mbr") : r.Id };
            e.Apply(r);
            db.Members.Add(e);
            existing.Add(e.Id);
            if (!string.IsNullOrEmpty(emailKey)) byEmail[emailKey] = e.Id;
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportCourses(AppDbContext db, List<CourseDto> rows)
    {
        var existing = (await db.Courses.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        // Nine FKs are checked against rows already committed to the DB —
        // import Nines first if a Course references them.
        var nineIds = (await db.Nines.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        // Natural key: course name (case-insensitive).
        var byName = (await db.Courses.AsNoTracking()
                .Select(c => new { c.Id, c.Name })
                .ToListAsync())
            .ToDictionary(c => c.Name.Trim().ToLowerInvariant(), c => c.Id);

        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Name))
            { errors.Add(new(i, r.Id, "required_field_missing", "Name required")); continue; }
            if (!string.IsNullOrEmpty(r.FrontNineId) && !nineIds.Contains(r.FrontNineId))
            { errors.Add(new(i, r.Id, "fk_missing", $"FrontNineId {r.FrontNineId} not found")); continue; }
            if (!string.IsNullOrEmpty(r.BackNineId) && !nineIds.Contains(r.BackNineId))
            { errors.Add(new(i, r.Id, "fk_missing", $"BackNineId {r.BackNineId} not found")); continue; }

            var nameKey = r.Name.Trim().ToLowerInvariant();
            if (byName.TryGetValue(nameKey, out var existingByName) && existingByName != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"course '{r.Name}' already exists as {existingByName}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Course { Id = string.IsNullOrEmpty(r.Id) ? NewId("crs") : r.Id };
            e.Apply(r);
            db.Courses.Add(e);
            existing.Add(e.Id);
            byName[nameKey] = e.Id;
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportNines(AppDbContext db, List<NineDto> rows)
    {
        var existing = (await db.Nines.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        // Natural key: Nine name (case-insensitive).
        var byName = (await db.Nines.AsNoTracking()
                .Select(n => new { n.Id, n.Name })
                .ToListAsync())
            .ToDictionary(n => n.Name.Trim().ToLowerInvariant(), n => n.Id);

        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Name))
            { errors.Add(new(i, r.Id, "required_field_missing", "Name required")); continue; }
            if (r.Holes is null || r.Holes.Count != 9)
            { errors.Add(new(i, r.Id, "invalid_hole_count", "A nine must include exactly 9 holes")); continue; }
            // Hole numbers must be the set {1..9}; the Nine editor in
            // the UI guarantees this, but uploaded files have not been
            // through it so we verify here.
            var nums = r.Holes.Select(h => h.Number).OrderBy(n => n).ToList();
            if (!nums.SequenceEqual(Enumerable.Range(1, 9)))
            { errors.Add(new(i, r.Id, "invalid_hole_numbers", "Hole numbers must be 1..9")); continue; }

            var nameKey = r.Name.Trim().ToLowerInvariant();
            if (byName.TryGetValue(nameKey, out var existingByName) && existingByName != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"nine '{r.Name}' already exists as {existingByName}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }

            var nineId = string.IsNullOrEmpty(r.Id) ? NewId("nin") : r.Id;
            var n = new Nine
            {
                Id = nineId,
                Name = r.Name,
                Description = r.Description ?? string.Empty,
                Notes = r.Notes ?? string.Empty
            };

            // Build a tee-id remap so user-supplied (or blank) tee-set
            // ids on the inbound DTO resolve to the persisted ids that
            // HoleYardage rows will reference.
            var teeIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
            var order = 0;
            foreach (var t in r.TeeSets ?? new List<NineTeeSetDto>())
            {
                var teeId = string.IsNullOrEmpty(t.Id) ? NewId("nts") : t.Id;
                if (!string.IsNullOrEmpty(t.Id)) teeIdMap[t.Id] = teeId;
                n.TeeSets.Add(new NineTeeSet
                {
                    Id = teeId,
                    NineId = nineId,
                    Name = t.Name ?? string.Empty,
                    Color = t.Color ?? string.Empty,
                    SortOrder = t.SortOrder == 0 ? order : t.SortOrder
                });
                order++;
            }

            foreach (var h in r.Holes)
            {
                var holeId = string.IsNullOrEmpty(h.Id) ? NewId("hol") : h.Id;
                var hole = new Hole
                {
                    Id = holeId,
                    NineId = nineId,
                    Number = h.Number,
                    Par = h.Par,
                    HandicapIndex = h.HandicapIndex,
                    Notes = h.Notes ?? string.Empty
                };
                foreach (var y in h.Yardages ?? new List<HoleYardageDto>())
                {
                    if (!teeIdMap.TryGetValue(y.TeeSetId, out var teeId))
                        teeId = y.TeeSetId;
                    if (string.IsNullOrEmpty(teeId)) continue;
                    hole.Yardages.Add(new HoleYardage
                    {
                        Id = string.IsNullOrEmpty(y.Id) ? NewId("hyd") : y.Id,
                        HoleId = holeId,
                        TeeSetId = teeId,
                        Yards = y.Yards
                    });
                }
                n.Holes.Add(hole);
            }
            db.Nines.Add(n);
            existing.Add(nineId);
            byName[nameKey] = nineId;
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
        // Natural key: (date, time, courseId). A "slot" is one row per
        // (date, time, courseId) — two groups at the same slot on the
        // same course is what tee sheet rows represent. Importing the
        // same row twice (e.g., re-running a script) is the common
        // re-run mistake we want to catch.
        var bySlot = (await db.TeeTimes.AsNoTracking()
                .Select(t => new { t.Id, t.Date, t.Time, t.CourseId })
                .ToListAsync())
            .ToDictionary(t => $"{t.Date}|{t.Time}|{t.CourseId}", t => t.Id);

        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Date) || string.IsNullOrWhiteSpace(r.Time))
            { errors.Add(new(i, r.Id, "required_field_missing", "Date and Time required")); continue; }
            if (!courseIds.Contains(r.CourseId))
            { errors.Add(new(i, r.Id, "fk_missing", $"CourseId {r.CourseId} not found")); continue; }

            var slotKey = $"{r.Date}|{r.Time}|{r.CourseId}";
            if (bySlot.TryGetValue(slotKey, out var existingBySlot) && existingBySlot != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"slot {r.Date} {r.Time} on {r.CourseId} already booked as {existingBySlot}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new TeeTime { Id = string.IsNullOrEmpty(r.Id) ? NewId("tee") : r.Id };
            e.Apply(r);
            db.TeeTimes.Add(e);
            existing.Add(e.Id);
            bySlot[slotKey] = e.Id;
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportStaff(AppDbContext db, List<StaffMemberDto> rows)
    {
        var existing = (await db.Staff.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        // Natural key: email (case-insensitive). Skip rows with no email.
        var byEmail = (await db.Staff.AsNoTracking()
                .Where(s => s.Email != "")
                .Select(s => new { s.Id, s.Email })
                .ToListAsync())
            .ToDictionary(s => s.Email.Trim().ToLowerInvariant(), s => s.Id);

        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.FirstName) || string.IsNullOrWhiteSpace(r.LastName))
            { errors.Add(new(i, r.Id, "required_field_missing", "FirstName and LastName required")); continue; }

            var emailKey = (r.Email ?? "").Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(emailKey)
                && byEmail.TryGetValue(emailKey, out var existingByEmail)
                && existingByEmail != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"email '{r.Email}' already used by {existingByEmail}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new StaffMember { Id = string.IsNullOrEmpty(r.Id) ? NewId("stf") : r.Id };
            e.Apply(r);
            db.Staff.Add(e);
            existing.Add(e.Id);
            if (!string.IsNullOrEmpty(emailKey)) byEmail[emailKey] = e.Id;
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
        // Natural key: (staffId, date, start, end). Same person can have
        // two non-overlapping shifts on the same day (e.g., split shift),
        // but not two with identical start+end times.
        var byShift = (await db.Shifts.AsNoTracking()
                .Select(s => new { s.Id, s.StaffId, s.Date, s.Start, s.End })
                .ToListAsync())
            .ToDictionary(s => $"{s.StaffId}|{s.Date}|{s.Start}|{s.End}", s => s.Id);

        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Date) || string.IsNullOrWhiteSpace(r.Start) || string.IsNullOrWhiteSpace(r.End))
            { errors.Add(new(i, r.Id, "required_field_missing", "Date, Start, End required")); continue; }
            if (!staffIds.Contains(r.StaffId))
            { errors.Add(new(i, r.Id, "fk_missing", $"StaffId {r.StaffId} not found")); continue; }

            var shiftKey = $"{r.StaffId}|{r.Date}|{r.Start}|{r.End}";
            if (byShift.TryGetValue(shiftKey, out var existingByShift) && existingByShift != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"shift for {r.StaffId} on {r.Date} {r.Start}-{r.End} already exists as {existingByShift}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Shift { Id = string.IsNullOrEmpty(r.Id) ? NewId("shft") : r.Id };
            e.Apply(r);
            db.Shifts.Add(e);
            existing.Add(e.Id);
            byShift[shiftKey] = e.Id;
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
        // Natural key: (staffId, dayOfWeek, start, end).
        var byTpl = (await db.WeeklyTemplates.AsNoTracking()
                .Select(t => new { t.Id, t.StaffId, t.DayOfWeek, t.Start, t.End })
                .ToListAsync())
            .ToDictionary(t => $"{t.StaffId}|{t.DayOfWeek}|{t.Start}|{t.End}", t => t.Id);

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

            var tplKey = $"{r.StaffId}|{r.DayOfWeek}|{r.Start}|{r.End}";
            if (byTpl.TryGetValue(tplKey, out var existingByTpl) && existingByTpl != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"weekly template for {r.StaffId} day {r.DayOfWeek} {r.Start}-{r.End} already exists as {existingByTpl}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new WeeklyTemplate { Id = string.IsNullOrEmpty(r.Id) ? NewId("wtmp") : r.Id };
            e.Apply(r);
            db.WeeklyTemplates.Add(e);
            existing.Add(e.Id);
            byTpl[tplKey] = e.Id;
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }

    private static async Task<ImportResult> ImportProducts(AppDbContext db, List<ProductDto> rows)
    {
        var existing = (await db.Products.AsNoTracking().Select(x => x.Id).ToListAsync()).ToHashSet();
        // Natural key: SKU (case-insensitive). SKUs are the catalog's
        // intended unique identifier; two products with the same SKU is
        // always a mistake.
        var bySku = (await db.Products.AsNoTracking()
                .Select(p => new { p.Id, p.Sku })
                .ToListAsync())
            .ToDictionary(p => p.Sku.Trim().ToLowerInvariant(), p => p.Id);

        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Sku))
            { errors.Add(new(i, r.Id, "required_field_missing", "Name and Sku required")); continue; }

            var skuKey = r.Sku.Trim().ToLowerInvariant();
            if (bySku.TryGetValue(skuKey, out var existingBySku) && existingBySku != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"SKU '{r.Sku}' already used by {existingBySku}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Product { Id = string.IsNullOrEmpty(r.Id) ? NewId("prod") : r.Id };
            e.Apply(r);
            db.Products.Add(e);
            existing.Add(e.Id);
            bySku[skuKey] = e.Id;
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
        // Natural key: (name, date). Same tournament name happens annually
        // (e.g., "Spring Junior Cup") so name alone isn't enough; the
        // pair is the actual identity.
        var byNameDate = (await db.Tournaments.AsNoTracking()
                .Select(t => new { t.Id, t.Name, t.Date })
                .ToListAsync())
            .ToDictionary(t => $"{t.Name.Trim().ToLowerInvariant()}|{t.Date}", t => t.Id);

        var (created, skipped, errors) = (0, 0, new List<ImportRowError>());
        await using var tx = await db.Database.BeginTransactionAsync();
        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.Date))
            { errors.Add(new(i, r.Id, "required_field_missing", "Name and Date required")); continue; }
            if (!courseIds.Contains(r.CourseId))
            { errors.Add(new(i, r.Id, "fk_missing", $"CourseId {r.CourseId} not found")); continue; }

            var tournKey = $"{r.Name.Trim().ToLowerInvariant()}|{r.Date}";
            if (byNameDate.TryGetValue(tournKey, out var existingByTourn) && existingByTourn != r.Id)
            {
                skipped++;
                errors.Add(new(i, r.Id, "duplicate_natural_key",
                    $"tournament '{r.Name}' on {r.Date} already exists as {existingByTourn}"));
                continue;
            }

            if (!string.IsNullOrEmpty(r.Id) && existing.Contains(r.Id))
            { skipped++; errors.Add(new(i, r.Id, "id_exists")); continue; }
            var e = new Tournament { Id = string.IsNullOrEmpty(r.Id) ? NewId("trn") : r.Id };
            e.Apply(r);
            db.Tournaments.Add(e);
            existing.Add(e.Id);
            byNameDate[tournKey] = e.Id;
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
            // The MaintenanceTask entity stores CourseId / AssignedTo as
            // NOT NULL TEXT columns, but the import schema documents both as
            // optional. Coerce omitted/null values to "" so a row without
            // those fields doesn't blow up SaveChanges with a NOT NULL
            // constraint violation. Apply() would otherwise propagate the
            // null straight to the column.
            var normalized = r with
            {
                CourseId = r.CourseId ?? string.Empty,
                AssignedTo = r.AssignedTo ?? string.Empty,
            };
            var e = new MaintenanceTask { Id = string.IsNullOrEmpty(r.Id) ? NewId("mnt") : r.Id };
            e.Apply(normalized);
            db.Maintenance.Add(e);
            existing.Add(e.Id);
            created++;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(created, skipped, errors);
    }
}
