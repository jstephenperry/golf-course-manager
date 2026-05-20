namespace FairwayHq.Api.Services;

/// <summary>
/// A9: Single shared money-rounding helper. All currency math that produces
/// a displayed/persisted amount routes through here so rounding is uniform
/// (2 decimals, away-from-zero) and there are no magic epsilon comparisons
/// scattered through the endpoints.
/// </summary>
public static class Money
{
    /// <summary>Round to cents using banker-free away-from-zero rounding.</summary>
    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>True when a rounded balance is strictly positive (i.e. money is owed).</summary>
    public static bool IsOwed(decimal balance) => Round(balance) > 0m;
}
