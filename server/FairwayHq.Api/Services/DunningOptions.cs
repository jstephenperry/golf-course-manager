namespace FairwayHq.Api.Services;

public class DunningOptions
{
    public const string Section = "Dunning";

    /// <summary>NET terms (in days). Default 60.</summary>
    public int PastDueDays { get; set; } = 60;

    /// <summary>How often the background sweep runs. Default 30 minutes.</summary>
    public int RunIntervalMinutes { get; set; } = 30;

    /// <summary>Run once on startup so manual restarts catch up. Default true.</summary>
    public bool RunOnStartup { get; set; } = true;
}
