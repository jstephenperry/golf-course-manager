using FairwayHq.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Services;

/// <summary>
/// A6: Small optimistic-concurrency retry loop for balance/stock mutating
/// paths. SQLite has no native rowversion, so entities carry an app-managed
/// integer Version marked .IsConcurrencyToken(). When two writers race, the
/// loser gets a DbUpdateConcurrencyException; we discard the stale tracked
/// state and re-run the action against fresh data.
/// </summary>
public static class ConcurrencyRetry
{
    public const int DefaultMaxAttempts = 5;

    public static async Task<T> ExecuteAsync<T>(
        AppDbContext db, Func<Task<T>> action, int maxAttempts = DefaultMaxAttempts)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxAttempts)
            {
                // Drop every tracked entity so the next attempt re-reads
                // ground truth from the database rather than replaying stale
                // values that just lost the race.
                foreach (var entry in db.ChangeTracker.Entries().ToList())
                {
                    entry.State = EntityState.Detached;
                }
            }
        }
    }
}
