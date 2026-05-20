namespace FairwayHq.Api.Services;

/// <summary>
/// A14: Config-driven bounds for the membership write paths.
/// </summary>
public class MembershipOptions
{
    public const string Section = "Membership";

    /// <summary>
    /// Upper bound on a member application's initiation fee. Defends against
    /// a fat-fingered (or malicious) fee that would post an enormous opening
    /// charge to a brand-new member's ledger on activation. Default $100,000.
    /// </summary>
    public decimal MaxInitiationFee { get; set; } = 100_000m;
}
