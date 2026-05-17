namespace FairwayHq.Api.Data;

/// <summary>
/// Seed bootstrap stub. The application ships with no synthetic data:
/// the user provides initial data via the bulk-import endpoints
/// (POST /api/import/<entity>) and uploads in the /import UI. This
/// method stays as a no-op so existing call sites (Program.cs and the
/// /api/reset Ops endpoint) compile and remain available as a future
/// extension point if an opt-in demo dataset is ever reintroduced.
/// </summary>
public static class Seed
{
    public static void EnsureSeeded(AppDbContext db)
    {
        // No-op. See class doc.
    }
}
