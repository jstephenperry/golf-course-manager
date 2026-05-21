using FairwayHq.Api.Models;

namespace FairwayHq.Api.Services;

/// <summary>
/// A14: Lightweight centralized input validation for the write paths.
/// Returns a machine-readable error code (matching the existing
/// { error = "..." } convention) or null when the payload is acceptable.
/// Kept dependency-free on purpose — no FluentValidation needed for this
/// surface area.
/// </summary>
public static class Validation
{
    // Allowed enumerations. Mirror the values the client/UI emit; unknown
    // strings are rejected rather than silently persisted.
    public static readonly HashSet<string> MemberTiers = new(StringComparer.Ordinal)
    {
        "Full", "Weekday", "Corporate", "Social", "Junior", "Senior",
    };

    public static readonly HashSet<string> MemberStatuses = new(StringComparer.Ordinal)
    {
        "Active", "Suspended", "Inactive",
    };

    public static string? ValidateMemberProfile(MemberUpdateDto d)
    {
        if (string.IsNullOrWhiteSpace(d.FirstName) || string.IsNullOrWhiteSpace(d.LastName))
            return "name_required";
        if (!string.IsNullOrEmpty(d.Tier) && !MemberTiers.Contains(d.Tier))
            return "unknown_tier";
        if (d.Handicap < 0)
            return "negative_handicap";
        return null;
    }

    public static string? ValidateMemberCreate(MemberDto d)
    {
        if (string.IsNullOrWhiteSpace(d.FirstName) || string.IsNullOrWhiteSpace(d.LastName))
            return "name_required";
        if (!string.IsNullOrEmpty(d.Tier) && !MemberTiers.Contains(d.Tier))
            return "unknown_tier";
        if (!string.IsNullOrEmpty(d.Status) && !MemberStatuses.Contains(d.Status))
            return "unknown_status";
        if (d.Handicap < 0)
            return "negative_handicap";
        if (d.Balance < 0)
            return "negative_balance";
        return null;
    }

    public static string? ValidateProduct(ProductDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Name))
            return "name_required";
        if (d.Price < 0 || d.Cost < 0)
            return "negative_price";
        if (d.Stock < 0 || d.ReorderLevel < 0)
            return "negative_stock";
        return null;
    }

    public static string? ValidateStaff(StaffMemberDto d)
    {
        if (string.IsNullOrWhiteSpace(d.FirstName) || string.IsNullOrWhiteSpace(d.LastName))
            return "name_required";
        if (d.HourlyRate < 0)
            return "negative_rate";
        return null;
    }

    public static string? ValidateTournament(TournamentDto d)
    {
        if (d.EntryFee < 0)
            return "negative_fee";
        if (d.MaxPlayers < 0)
            return "negative_max_players";
        return null;
    }
}
