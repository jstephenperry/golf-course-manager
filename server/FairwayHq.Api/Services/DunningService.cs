using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FairwayHq.Api.Services;

/// <summary>
/// Background sweep that auto-suspends Active members whose oldest unpaid
/// charge is past the configured NET-X grace period (default 60 days).
/// Also reinstates Suspended members whose balance has returned to zero
/// (defense in depth — payment endpoints already handle the happy path).
/// </summary>
public class DunningService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IOptionsMonitor<DunningOptions> _options;
    private readonly ILogger<DunningService> _logger;

    public DunningService(
        IServiceProvider services,
        IOptionsMonitor<DunningOptions> options,
        ILogger<DunningService> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.CurrentValue.RunOnStartup)
        {
            await SafeRunAsync(stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var delayMinutes = Math.Max(1, _options.CurrentValue.RunIntervalMinutes);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(delayMinutes), stoppingToken);
            }
            catch (TaskCanceledException) { break; }

            await SafeRunAsync(stoppingToken);
        }
    }

    private async Task SafeRunAsync(CancellationToken ct)
    {
        try
        {
            var result = await RunOnceAsync(ct);
            if (result.Suspended > 0 || result.Reinstated > 0)
            {
                _logger.LogInformation(
                    "Dunning sweep: suspended {Suspended}, reinstated {Reinstated}.",
                    result.Suspended, result.Reinstated);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dunning sweep failed");
        }
    }

    public async Task<DunningRunResultDto> RunOnceAsync(CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await RunOnceAsync(db, _options.CurrentValue, DateTime.UtcNow, ct);
    }

    /// <summary>
    /// Static, testable entry point. Reads members from the supplied DbContext
    /// and applies suspension/reinstatement transitions in-place.
    /// </summary>
    public static async Task<DunningRunResultDto> RunOnceAsync(
        AppDbContext db,
        DunningOptions options,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var cutoff = nowUtc.AddDays(-Math.Max(0, options.PastDueDays));
        var affected = new List<string>();
        var suspended = 0;
        var reinstated = 0;

        var members = await db.Members.ToListAsync(ct);
        foreach (var m in members)
        {
            // Suspend: active member with an aging timestamp older than cutoff.
            if (m.Status == "Active"
                && !string.IsNullOrEmpty(m.OldestUnpaidChargeAt)
                && DateTime.TryParse(m.OldestUnpaidChargeAt, out var since)
                && since.ToUniversalTime() <= cutoff
                && m.Balance > 0)
            {
                m.Status = "Suspended";
                m.Active = false;
                m.SuspendedAt = nowUtc.ToString("o");
                suspended++;
                affected.Add(m.Id);
                continue;
            }

            // Defensive reinstate: paid off but still flagged.
            if (m.Status == "Suspended" && m.Balance <= 0)
            {
                m.Status = "Active";
                m.Active = true;
                m.SuspendedAt = null;
                m.OldestUnpaidChargeAt = null;
                reinstated++;
                affected.Add(m.Id);
            }
        }

        if (suspended > 0 || reinstated > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return new DunningRunResultDto(suspended, reinstated, affected.ToArray());
    }
}
