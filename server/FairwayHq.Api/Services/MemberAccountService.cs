using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FairwayHq.Api.Services;

/// <summary>
/// Centralizes member-balance side effects. Anything that changes a
/// member's balance must go through one of the Post* / Void* helpers so
/// the ledger (source of truth) and Member.Balance / OldestUnpaidChargeAt
/// caches stay consistent. Callers are expected to manage a transaction
/// around their batch of helper calls; the helpers do not call
/// SaveChanges themselves.
/// </summary>
public static class MemberAccountService
{
    public static class LedgerCategories
    {
        // "Payment" is reserved for the synthetic category stamped on
        // payment-type entries — manual charge posts must reject it.
        public static readonly HashSet<string> ChargeAllowed = new(StringComparer.Ordinal)
        {
            "Dues", "F&B", "ProShop", "Tournament", "Initiation", "Lesson", "Adjustment",
        };
        public const string PaymentCategory = "Payment";
        public static readonly HashSet<string> All = new(ChargeAllowed) { PaymentCategory };
    }

    public static class LedgerMethods
    {
        public static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
        {
            "Cash", "Card", "Check", "ACH",
        };
    }

    public record PostResult(MemberLedgerEntry? Entry, string? Error);

    private static string NewId() =>
        $"led_{Guid.NewGuid():N}".Substring(0, 16);

    // ----- ledger-aware helpers (the chokepoint) -----
    // These insert a MemberLedgerEntry and update Member.Balance +
    // OldestUnpaidChargeAt atomically. They expect to be called inside a
    // transaction managed by the caller; they do not call SaveChanges.

    /// <summary>
    /// Post a Charge entry to the ledger and increment the member's
    /// cached balance. Stamps OldestUnpaidChargeAt if this is the first
    /// charge in the current debt cycle.
    /// </summary>
    public static PostResult PostCharge(
        AppDbContext db,
        Member member,
        decimal amount,
        string category,
        string sourceKind,
        string? sourceId,
        string note,
        DateTime nowUtc)
    {
        if (amount <= 0) return new(null, "amount_must_be_positive");
        if (!LedgerCategories.ChargeAllowed.Contains(category))
            return new(null, "unknown_category");

        var entry = new MemberLedgerEntry
        {
            Id = NewId(),
            MemberId = member.Id,
            EntryType = "Charge",
            Category = category,
            Amount = amount,
            Method = null,
            Note = note,
            PostedAt = nowUtc.ToString("o"),
            SourceKind = sourceKind,
            SourceId = sourceId,
        };
        db.MemberLedgerEntries.Add(entry);

        var hadBalance = member.Balance > 0m;
        member.Balance += amount;
        member.Version++; // A6: bump concurrency token on every balance mutation
        if (!hadBalance)
        {
            member.OldestUnpaidChargeAt = entry.PostedAt;
        }
        return new(entry, null);
    }

    /// <summary>
    /// Post a Payment entry. Decrements the cached balance (clamped at
    /// zero). Clears OldestUnpaidChargeAt + auto-reinstates a previously
    /// auto-suspended member when balance returns to zero.
    /// </summary>
    public static PostResult PostPayment(
        AppDbContext db,
        Member member,
        decimal amount,
        string? method,
        string sourceKind,
        string? sourceId,
        string note,
        DateTime nowUtc)
    {
        if (amount <= 0) return new(null, "amount_must_be_positive");
        if (method is not null && !LedgerMethods.Allowed.Contains(method))
            return new(null, "unknown_method");

        var entry = new MemberLedgerEntry
        {
            Id = NewId(),
            MemberId = member.Id,
            EntryType = "Payment",
            Category = LedgerCategories.PaymentCategory,
            Amount = amount,
            Method = method,
            Note = note,
            PostedAt = nowUtc.ToString("o"),
            SourceKind = sourceKind,
            SourceId = sourceId,
        };
        db.MemberLedgerEntries.Add(entry);

        member.Balance = Math.Max(0m, member.Balance - amount);
        member.Version++; // A6
        ApplyBalanceClearedSideEffects(member);
        return new(entry, null);
    }

    /// <summary>
    /// Voids a prior entry by appending an immutable Reversal entry.
    /// The original entry stays intact; VoidedAt + VoidedByEntryId are
    /// set so we can't double-void. Tab-sourced entries cannot be voided
    /// through here — use the tab void endpoint instead.
    /// </summary>
    public static PostResult VoidEntry(
        AppDbContext db,
        MemberLedgerEntry original,
        Member member,
        string note,
        DateTime nowUtc)
    {
        if (original.VoidedAt is not null) return new(null, "already_voided");
        if (original.EntryType == "Reversal") return new(null, "cannot_void_reversal");
        if (original.SourceKind == "Tab") return new(null, "void_via_tab");

        var reversal = new MemberLedgerEntry
        {
            Id = NewId(),
            MemberId = original.MemberId,
            EntryType = "Reversal",
            Category = original.Category,
            Amount = original.Amount,
            Method = original.Method,
            Note = note,
            PostedAt = nowUtc.ToString("o"),
            SourceKind = "Manual",
            SourceId = null,
            ReversesEntryId = original.Id,
        };
        db.MemberLedgerEntries.Add(reversal);

        original.VoidedAt = reversal.PostedAt;
        original.VoidedByEntryId = reversal.Id;
        member.Version++; // A6

        // Reverse the original's effect on the cached balance.
        if (original.EntryType == "Charge")
        {
            // Charge cancelled: credit the balance.
            member.Balance = Math.Max(0m, member.Balance - original.Amount);
            ApplyBalanceClearedSideEffects(member);
        }
        else // EntryType == "Payment"
        {
            // Payment cancelled: re-debit the balance.
            member.Balance += original.Amount;
            // The original "cycle start" may need to re-stamp if this
            // payment had cleared the balance. Recompute from ledger to
            // ground truth.
            member.OldestUnpaidChargeAt = ComputeOldestUnpaid(db, member.Id);
        }
        return new(reversal, null);
    }

    /// <summary>
    /// Computes the PostedAt of the entry that opened the current debt
    /// cycle (i.e., the earliest debit-type event after which the
    /// running balance stayed positive through every subsequent entry).
    /// Returns null when the cumulative balance is zero. Used for cache
    /// sanity-tests and to re-stamp after voiding a payment.
    /// </summary>
    public static string? ComputeOldestUnpaid(AppDbContext db, string memberId)
    {
        // Include unsaved tracked entries so calls made *between* helper
        // invocations and SaveChanges see the right view. Use ChangeTracker
        // for added-but-unsaved entries; SQLite already exposes saved
        // tracked rows through the DbSet without needing AsNoTracking.
        var saved = db.MemberLedgerEntries
            .Where(e => e.MemberId == memberId)
            .ToList();
        var pending = db.ChangeTracker.Entries<MemberLedgerEntry>()
            .Where(e => e.State == EntityState.Added
                        && e.Entity.MemberId == memberId)
            .Select(e => e.Entity)
            .ToList();

        var entries = saved
            .Concat(pending.Where(p => !saved.Any(s => s.Id == p.Id)))
            .OrderBy(e => e.PostedAt, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();

        var byId = entries.ToDictionary(e => e.Id);

        decimal Delta(MemberLedgerEntry e) => e.EntryType switch
        {
            "Charge" => e.Amount,
            "Payment" => -e.Amount,
            "Reversal" when e.ReversesEntryId is { } rid && byId.TryGetValue(rid, out var orig) =>
                orig.EntryType == "Charge" ? -e.Amount : e.Amount,
            _ => 0m,
        };

        decimal balance = 0m;
        string? cycleStart = null;
        foreach (var e in entries)
        {
            var prev = balance;
            balance += Delta(e);
            if (prev <= 0m && balance > 0m) cycleStart = e.PostedAt;
            if (balance <= 0m) cycleStart = null;
        }
        return cycleStart;
    }

    /// <summary>
    /// A2: Reconstructs a member's balance purely from a set of ledger
    /// entries, using the same Charge/Payment/Reversal semantics as the live
    /// Post*/Void* helpers. Clamped at zero to mirror PostPayment. Used by
    /// snapshot restore to ground Member.Balance in the ledger rather than
    /// trusting the (potentially tampered) MemberDto.Balance.
    /// </summary>
    public static decimal ComputeBalanceFromLedger(IEnumerable<MemberLedgerEntry> entries)
    {
        var list = entries
            .OrderBy(e => e.PostedAt, StringComparer.Ordinal)
            .ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();
        var byId = list.ToDictionary(e => e.Id);

        decimal Delta(MemberLedgerEntry e) => e.EntryType switch
        {
            "Charge" => e.Amount,
            "Payment" => -e.Amount,
            "Reversal" when e.ReversesEntryId is { } rid && byId.TryGetValue(rid, out var orig) =>
                orig.EntryType == "Charge" ? -e.Amount : e.Amount,
            _ => 0m,
        };

        decimal balance = 0m;
        foreach (var e in list)
        {
            balance += Delta(e);
            // Payments clamp the running balance at zero (mirrors PostPayment),
            // so a credit beyond zero doesn't create a negative carry.
            if (balance < 0m) balance = 0m;
        }
        return balance;
    }

    private static void ApplyBalanceClearedSideEffects(Member member)
    {
        if (member.Balance > 0m) return;
        member.OldestUnpaidChargeAt = null;
        if (member.SuspendedAt is not null)
        {
            member.SuspendedAt = null;
            if (member.Status == "Suspended")
            {
                member.Status = "Active";
                member.Active = true;
            }
        }
    }
}
