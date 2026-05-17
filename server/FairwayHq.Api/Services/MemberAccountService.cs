using FairwayHq.Api.Models;

namespace FairwayHq.Api.Services;

/// <summary>
/// Centralizes member-balance side effects. Anything that changes a member's
/// balance should go through ChargeMember / CreditMember so that the aging
/// timestamp (OldestUnpaidChargeAt) and auto-reinstate logic stay consistent.
/// </summary>
public static class MemberAccountService
{
    /// <summary>
    /// Apply a positive charge to the member's balance. Stamps the aging
    /// timestamp if the member had no outstanding balance prior to this charge.
    /// </summary>
    public static void ChargeMember(Member member, decimal amount, DateTime nowUtc)
    {
        if (amount <= 0) return;
        var hadBalance = member.Balance > 0m;
        member.Balance += amount;
        if (!hadBalance)
        {
            member.OldestUnpaidChargeAt = nowUtc.ToString("o");
        }
    }

    /// <summary>
    /// Reverse a charge (or apply a credit). Balance clamps at zero. If the
    /// balance reaches zero, the aging timestamp + auto-suspension state are
    /// cleared and any auto-suspended member is reinstated.
    /// </summary>
    public static void CreditMember(Member member, decimal amount)
    {
        if (amount <= 0) return;
        member.Balance = Math.Max(0m, member.Balance - amount);
        if (member.Balance <= 0m)
        {
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
}
