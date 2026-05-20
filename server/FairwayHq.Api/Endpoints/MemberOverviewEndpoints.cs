using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class MemberOverviewEndpoints
{
    private const int RecentRoundsLimit = 10;

    public static void MapMemberOverview(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/members/{id}/overview",
            async (string id, AppDbContext db) =>
            {
                var member = await db.Members.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == id);
                if (member is null) return Results.NotFound();

                // Load all tee times for this member once, then partition.
                // PlayersJson is a serialized string[] with no index; a LIKE
                // filter would be fragile (substring false positives) and
                // isn't truly indexable. At expected scale (~30k rounds/year
                // for a single course) the in-memory scan is fine; the move
                // when this hurts is a normalized TeeTimePlayer join table.
                var memberTeeTimes = (await db.TeeTimes.AsNoTracking().ToListAsync())
                    .Select(t => t.ToDto())
                    .Where(t => t.Players.Contains(id))
                    .ToList();

                var completed = memberTeeTimes
                    .Where(t => t.Status == "Completed")
                    .OrderByDescending(t => t.Date)
                    .ThenByDescending(t => t.Time)
                    .ToList();

                var noShowCount = memberTeeTimes.Count(t => t.Status == "No Show");

                return Results.Ok(new MemberOverviewDto(
                    Member: member.ToDto(),
                    LastPlayedDate: completed.FirstOrDefault()?.Date,
                    LifetimeRounds: completed.Count,
                    NoShowCount: noShowCount,
                    RecentRounds: completed.Take(RecentRoundsLimit).ToList()
                ));
            }
        ).WithTags("Members").RequireAuthorization(Policy.For(Permissions.MembersOverviewRead));
    }
}
