using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

// CRUD for a Nine and the tee sets, holes, and per-tee yardages nested
// inside it. The Nine is the unit of editing: GET returns the full
// structure; PUT replaces the entire structure (tee sets + 9 holes +
// yardages) in one transactional write. This keeps the client form
// simple — render one editor for the whole Nine, send it back, the
// server reconciles it — and avoids the explosion of fine-grained
// endpoints a per-hole or per-yardage CRUD would require.
public static class NinesEndpoints
{
    private static string NewId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 1 + 12);

    public static void MapNines(this IEndpointRouteBuilder app)
    {
        var nines = app.MapGroup("/api/nines").WithTags("Nines");

        nines.MapGet("/", async (AppDbContext db) =>
        {
            var list = await db.Nines
                .Include(n => n.TeeSets)
                .Include(n => n.Holes).ThenInclude(h => h.Yardages)
                .AsNoTracking()
                .OrderBy(n => n.Name)
                .ToListAsync();
            return Results.Ok(list.Select(n => n.ToDto()));
        });

        nines.MapGet("/{id}", async (string id, AppDbContext db) =>
        {
            var n = await db.Nines
                .Include(x => x.TeeSets)
                .Include(x => x.Holes).ThenInclude(h => h.Yardages)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            return n is null ? Results.NotFound() : Results.Ok(n.ToDto());
        });

        nines.MapPost("/", async (NineDto dto, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return Results.BadRequest(new { error = "name_required" });

            var n = new Nine { Id = string.IsNullOrEmpty(dto.Id) ? NewId("n") : dto.Id };
            ApplyStructure(n, dto);
            db.Nines.Add(n);
            await db.SaveChangesAsync();
            return Results.Created($"/api/nines/{n.Id}",
                (await LoadDto(db, n.Id))!);
        });

        nines.MapPut("/{id}", async (string id, NineDto dto, AppDbContext db) =>
        {
            var existing = await db.Nines
                .Include(n => n.TeeSets)
                .Include(n => n.Holes).ThenInclude(h => h.Yardages)
                .FirstOrDefaultAsync(n => n.Id == id);
            if (existing is null) return Results.NotFound();

            // Wipe & replace nested structure. Cascade-delete handles the
            // children; we redirect to PUT-style overwrite to keep the
            // client contract simple.
            db.HoleYardages.RemoveRange(existing.Holes.SelectMany(h => h.Yardages));
            db.Holes.RemoveRange(existing.Holes);
            db.NineTeeSets.RemoveRange(existing.TeeSets);
            existing.Holes.Clear();
            existing.TeeSets.Clear();

            ApplyStructure(existing, dto);
            await db.SaveChangesAsync();
            return Results.Ok((await LoadDto(db, id))!);
        });

        nines.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var n = await db.Nines.FindAsync(id);
            if (n is null) return Results.NotFound();

            // Block deletion if any Course still references this Nine —
            // matches the Restrict cascade on Course.FrontNineId / BackNineId.
            var inUse = await db.Courses
                .AnyAsync(c => c.FrontNineId == id || c.BackNineId == id);
            if (inUse)
                return Results.BadRequest(new { error = "nine_in_use" });

            db.Nines.Remove(n);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    // Reconciles the inbound DTO onto a (cleared) Nine, generating ids
    // for new tee sets / holes / yardages where the client didn't supply
    // one. Tee-set ids referenced by yardages are remapped from
    // client-supplied ids to whatever id the tee set ends up with.
    private static void ApplyStructure(Nine n, NineDto dto)
    {
        n.Name = dto.Name;
        n.Description = dto.Description ?? string.Empty;
        n.Notes = dto.Notes ?? string.Empty;

        // teeIdMap: incoming TeeSetDto.Id (may be blank / temp) → final id
        var teeIdMap = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = 0;
        foreach (var t in dto.TeeSets ?? new List<NineTeeSetDto>())
        {
            var finalId = string.IsNullOrEmpty(t.Id) ? NewId("nts") : t.Id;
            if (!string.IsNullOrEmpty(t.Id)) teeIdMap[t.Id] = finalId;
            n.TeeSets.Add(new NineTeeSet
            {
                Id = finalId,
                NineId = n.Id,
                Name = t.Name ?? string.Empty,
                Color = t.Color ?? string.Empty,
                SortOrder = t.SortOrder == 0 ? order : t.SortOrder
            });
            order++;
        }

        foreach (var h in dto.Holes ?? new List<HoleDto>())
        {
            var holeId = string.IsNullOrEmpty(h.Id) ? NewId("h") : h.Id;
            var hole = new Hole
            {
                Id = holeId,
                NineId = n.Id,
                Number = h.Number,
                Par = h.Par,
                HandicapIndex = h.HandicapIndex,
                Notes = h.Notes ?? string.Empty
            };
            foreach (var y in h.Yardages ?? new List<HoleYardageDto>())
            {
                // Skip yardage rows pointing at a tee set we don't know
                // about — keeps the write defensive against stale forms.
                if (!teeIdMap.TryGetValue(y.TeeSetId, out var teeId))
                    teeId = y.TeeSetId;
                if (string.IsNullOrEmpty(teeId)) continue;
                hole.Yardages.Add(new HoleYardage
                {
                    Id = string.IsNullOrEmpty(y.Id) ? NewId("hy") : y.Id,
                    HoleId = holeId,
                    TeeSetId = teeId,
                    Yards = y.Yards
                });
            }
            n.Holes.Add(hole);
        }
    }

    private static async Task<NineDto?> LoadDto(AppDbContext db, string id)
    {
        var n = await db.Nines
            .Include(x => x.TeeSets)
            .Include(x => x.Holes).ThenInclude(h => h.Yardages)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
        return n?.ToDto();
    }
}
