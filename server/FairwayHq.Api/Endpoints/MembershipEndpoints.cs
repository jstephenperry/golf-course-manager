using System.Security.Claims;
using FairwayHq.Api.Authorization;
using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FairwayHq.Api.Endpoints;

public static class MembershipEndpoints
{
    private static string NewId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 1 + 12);

    // A4: derive the reviewer identity from the authenticated principal,
    // never from the request body. preferred_username (Keycloak) → name →
    // sub. In the Testing env the TestAuthHandler stamps all three.
    private static string ReviewerFrom(ClaimsPrincipal user) =>
        user.FindFirst("preferred_username")?.Value
        ?? user.Identity?.Name
        ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? "unknown";

    public static void MapMembership(this IEndpointRouteBuilder app)
    {
        var apps = app.MapGroup("/api/applications").WithTags("MemberApplications");

        apps.MapGet("/", async (int? offset, int? limit, AppDbContext db) =>
        {
            // A11: offset/limit pagination with backward-compatible defaults.
            var (skip, take) = CrudEndpoints.PageParams(offset, limit);
            return (await db.MemberApplications.AsNoTracking()
                .OrderByDescending(a => a.SubmittedAt).ThenBy(a => a.Id)
                .Skip(skip).Take(take)
                .ToListAsync())
                .Select(a => a.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersApplicationsRead));

        apps.MapPost("/", async (MemberApplicationDto dto, IOptions<MembershipOptions> opts, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            {
                return Results.BadRequest(new { error = "name_required" });
            }
            // A14: bound the initiation fee against a sane config-driven
            // ceiling and reject negatives before they become an opening
            // ledger charge on activation.
            if (dto.InitiationFee < 0)
                return Results.BadRequest(new { error = "negative_initiation_fee" });
            if (dto.InitiationFee > opts.Value.MaxInitiationFee)
                return Results.BadRequest(new { error = "initiation_fee_too_large" });
            if (!string.IsNullOrEmpty(dto.RequestedTier)
                && !Validation.MemberTiers.Contains(dto.RequestedTier))
                return Results.BadRequest(new { error = "unknown_tier" });

            var entity = new MemberApplication
            {
                Id = string.IsNullOrEmpty(dto.Id) ? NewId("app") : dto.Id,
                FirstName = dto.FirstName.Trim(),
                LastName = dto.LastName.Trim(),
                Email = dto.Email?.Trim() ?? string.Empty,
                Phone = dto.Phone?.Trim() ?? string.Empty,
                RequestedTier = string.IsNullOrEmpty(dto.RequestedTier) ? "Full" : dto.RequestedTier,
                SponsoringMemberId = dto.SponsoringMemberId,
                InitiationFee = dto.InitiationFee,
                Notes = dto.Notes ?? string.Empty,
                Status = "Pending",
                SubmittedAt = DateTime.UtcNow.ToString("o"),
            };
            db.MemberApplications.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/api/applications/{entity.Id}", entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersApplicationsWrite));

        apps.MapPut("/{id}", async (string id, MemberApplicationDto dto, IOptions<MembershipOptions> opts, AppDbContext db) =>
        {
            if (dto.InitiationFee < 0)
                return Results.BadRequest(new { error = "negative_initiation_fee" });
            if (dto.InitiationFee > opts.Value.MaxInitiationFee)
                return Results.BadRequest(new { error = "initiation_fee_too_large" });
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.Status != "Pending")
                return Results.BadRequest(new { error = "application_not_pending" });
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersApplicationsWrite));

        apps.MapPost("/{id}/approve", async (string id, [FromBody] ApplicationReviewDto body, ClaimsPrincipal user, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.Status != "Pending")
                return Results.BadRequest(new { error = "application_not_pending" });

            entity.Status = "Approved";
            entity.ReviewedAt = DateTime.UtcNow.ToString("o");
            // A4: reviewer is server-stamped from the principal; body.Reviewer is ignored.
            entity.ReviewedBy = ReviewerFrom(user);
            entity.ReviewNote = body?.Note;
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersApplicationsWrite));

        apps.MapPost("/{id}/reject", async (string id, [FromBody] ApplicationReviewDto body, ClaimsPrincipal user, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.Status != "Pending")
                return Results.BadRequest(new { error = "application_not_pending" });

            entity.Status = "Rejected";
            entity.ReviewedAt = DateTime.UtcNow.ToString("o");
            // A4: reviewer is server-stamped from the principal; body.Reviewer is ignored.
            entity.ReviewedBy = ReviewerFrom(user);
            entity.ReviewNote = body?.Note;
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersApplicationsWrite));

        apps.MapPost("/{id}/activate", async (string id, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.Status != "Approved")
                return Results.BadRequest(new { error = "application_not_approved" });

            await using var tx = await db.Database.BeginTransactionAsync();
            var member = new Member
            {
                Id = NewId("m"),
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                Email = entity.Email,
                Phone = entity.Phone,
                Tier = entity.RequestedTier,
                Handicap = 0,
                JoinDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                Status = "Active",
                Active = true,
                Balance = 0m,
            };

            db.Members.Add(member);

            // Roll the initiation fee onto the new member's account as the
            // first charge, kicking off the NET-X timer. Posts a ledger
            // entry tagged with the originating application id so the
            // member's account history shows the source.
            if (entity.InitiationFee > 0)
            {
                MemberAccountService.PostCharge(
                    db, member, entity.InitiationFee,
                    category: "Initiation",
                    sourceKind: "Application",
                    sourceId: entity.Id,
                    note: "Initiation fee",
                    nowUtc: DateTime.UtcNow);
            }

            entity.Status = "Activated";
            entity.ActivatedMemberId = member.Id;
            entity.ReviewedAt ??= DateTime.UtcNow.ToString("o");

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return Results.Ok(new
            {
                application = entity.ToDto(),
                member = member.ToDto(),
            });
        }).RequireAuthorization(Policy.For(Permissions.MembersApplicationsWrite));

        apps.MapPost("/{id}/withdraw", async (string id, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.Status == "Activated")
                return Results.BadRequest(new { error = "application_already_activated" });
            entity.Status = "Withdrawn";
            entity.ReviewedAt ??= DateTime.UtcNow.ToString("o");
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersApplicationsWrite));

        apps.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.MemberApplications.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(Policy.For(Permissions.MembersApplicationsWrite));

        // -------- Member account actions (suspend / reinstate) --------
        var members = app.MapGroup("/api/members").WithTags("Members");

        members.MapPost("/{id}/suspend", async (string id, [FromBody] ApplicationReviewDto? body, AppDbContext db) =>
        {
            var m = await db.Members.FindAsync(id);
            if (m is null) return Results.NotFound();
            m.Status = "Suspended";
            m.Active = false;
            m.SuspendedAt = DateTime.UtcNow.ToString("o");
            await db.SaveChangesAsync();
            return Results.Ok(m.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersSuspend));

        members.MapPost("/{id}/reinstate", async (string id, AppDbContext db) =>
        {
            var m = await db.Members.FindAsync(id);
            if (m is null) return Results.NotFound();
            m.Status = "Active";
            m.Active = true;
            m.SuspendedAt = null;
            if (m.Balance <= 0)
            {
                m.OldestUnpaidChargeAt = null;
            }
            await db.SaveChangesAsync();
            return Results.Ok(m.ToDto());
        }).RequireAuthorization(Policy.For(Permissions.MembersSuspend));

        // -------- Dunning trigger --------
        app.MapPost("/api/dunning/run", async (DunningService svc, CancellationToken ct) =>
        {
            var result = await svc.RunOnceAsync(ct);
            return Results.Ok(result);
        }).WithTags("Dunning").RequireAuthorization(Policy.For(Permissions.DunningRun));
    }
}
