using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Endpoints;

public static class MembershipEndpoints
{
    private static string NewId(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}".Substring(0, prefix.Length + 1 + 12);

    public static void MapMembership(this IEndpointRouteBuilder app)
    {
        var apps = app.MapGroup("/api/applications").WithTags("MemberApplications");

        apps.MapGet("/", async (AppDbContext db) =>
            (await db.MemberApplications.AsNoTracking().ToListAsync())
                .OrderByDescending(a => a.SubmittedAt)
                .Select(a => a.ToDto()));

        apps.MapPost("/", async (MemberApplicationDto dto, AppDbContext db) =>
        {
            if (string.IsNullOrWhiteSpace(dto.FirstName) || string.IsNullOrWhiteSpace(dto.LastName))
            {
                return Results.BadRequest(new { error = "name_required" });
            }

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
        });

        apps.MapPut("/{id}", async (string id, MemberApplicationDto dto, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.Status != "Pending")
                return Results.BadRequest(new { error = "application_not_pending" });
            entity.Apply(dto);
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        });

        apps.MapPost("/{id}/approve", async (string id, [FromBody] ApplicationReviewDto body, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.Status != "Pending")
                return Results.BadRequest(new { error = "application_not_pending" });

            entity.Status = "Approved";
            entity.ReviewedAt = DateTime.UtcNow.ToString("o");
            entity.ReviewedBy = body?.Reviewer;
            entity.ReviewNote = body?.Note;
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        });

        apps.MapPost("/{id}/reject", async (string id, [FromBody] ApplicationReviewDto body, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            if (entity.Status != "Pending")
                return Results.BadRequest(new { error = "application_not_pending" });

            entity.Status = "Rejected";
            entity.ReviewedAt = DateTime.UtcNow.ToString("o");
            entity.ReviewedBy = body?.Reviewer;
            entity.ReviewNote = body?.Note;
            await db.SaveChangesAsync();
            return Results.Ok(entity.ToDto());
        });

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

            // Roll the initiation fee onto the new member's account as the
            // first charge, kicking off the NET-X timer.
            if (entity.InitiationFee > 0)
            {
                MemberAccountService.ChargeMember(member, entity.InitiationFee, DateTime.UtcNow);
            }
            db.Members.Add(member);

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
        });

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
        });

        apps.MapDelete("/{id}", async (string id, AppDbContext db) =>
        {
            var entity = await db.MemberApplications.FindAsync(id);
            if (entity is null) return Results.NotFound();
            db.MemberApplications.Remove(entity);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

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
        });

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
        });

        // -------- Dunning trigger --------
        app.MapPost("/api/dunning/run", async (DunningService svc, CancellationToken ct) =>
        {
            var result = await svc.RunOnceAsync(ct);
            return Results.Ok(result);
        }).WithTags("Dunning");
    }
}
