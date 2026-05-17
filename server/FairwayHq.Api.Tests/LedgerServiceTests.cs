using FairwayHq.Api.Data;
using FairwayHq.Api.Models;
using FairwayHq.Api.Services;

namespace FairwayHq.Api.Tests;

/// <summary>
/// Service-level tests for the new ledger-aware helpers in
/// MemberAccountService. These exercise the helpers directly against a
/// DbContext rather than through HTTP. The HTTP-shaped tests live in
/// LedgerTests (added in A5).
/// </summary>
public class LedgerServiceTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;
    public LedgerServiceTests(ApiFactory factory) => _factory = factory;

    private static DateTime At(int dayOffset, int hour = 12) =>
        new DateTime(2026, 1, 1, hour, 0, 0, DateTimeKind.Utc).AddDays(dayOffset);

    private async Task<(AppDbContext db, Member member)> NewMemberAsync(
        string idSuffix, decimal startingBalance = 0m)
    {
        var db = _factory.CreateDbContext();
        var member = new Member
        {
            Id = $"led_test_{idSuffix}",
            FirstName = "Ledger",
            LastName = idSuffix,
            Email = $"{idSuffix}@example.com",
            Phone = "555",
            Tier = "Full",
            Handicap = 0,
            JoinDate = "2024-01-01",
            Active = true,
            Status = "Active",
            Balance = startingBalance,
        };
        db.Members.Add(member);
        await db.SaveChangesAsync();
        return (db, member);
    }

    [Fact]
    public async Task PostCharge_increments_balance_and_stamps_oldest_unpaid_first_time()
    {
        var (db, m) = await NewMemberAsync("charge-stamp");

        var res = MemberAccountService.PostCharge(
            db, m, 50m, "F&B", "Manual", null, "first charge", At(0));
        await db.SaveChangesAsync();

        Assert.Null(res.Error);
        Assert.NotNull(res.Entry);
        Assert.Equal("Charge", res.Entry!.EntryType);
        Assert.Equal(50m, m.Balance);
        Assert.Equal(res.Entry.PostedAt, m.OldestUnpaidChargeAt);
    }

    [Fact]
    public async Task PostCharge_does_not_restamp_oldest_unpaid_when_member_already_owes()
    {
        var (db, m) = await NewMemberAsync("charge-no-restamp");

        var first = MemberAccountService.PostCharge(
            db, m, 50m, "F&B", "Manual", null, "first", At(0));
        await db.SaveChangesAsync();
        var firstStamp = m.OldestUnpaidChargeAt;

        MemberAccountService.PostCharge(
            db, m, 25m, "Dues", "Manual", null, "second", At(1));
        await db.SaveChangesAsync();

        Assert.Equal(75m, m.Balance);
        Assert.Equal(firstStamp, m.OldestUnpaidChargeAt);
    }

    [Fact]
    public async Task PostCharge_rejects_unknown_category()
    {
        var (db, m) = await NewMemberAsync("bad-cat");
        var res = MemberAccountService.PostCharge(
            db, m, 10m, "Bogus", "Manual", null, "", At(0));
        Assert.Equal("unknown_category", res.Error);
        Assert.Null(res.Entry);
    }

    [Fact]
    public async Task PostCharge_rejects_synthetic_Payment_category()
    {
        var (db, m) = await NewMemberAsync("payment-cat");
        var res = MemberAccountService.PostCharge(
            db, m, 10m, "Payment", "Manual", null, "", At(0));
        Assert.Equal("unknown_category", res.Error);
    }

    [Fact]
    public async Task PostPayment_decrements_balance_and_clears_oldest_unpaid()
    {
        var (db, m) = await NewMemberAsync("pay-clear");

        MemberAccountService.PostCharge(db, m, 50m, "F&B", "Manual", null, "", At(0));
        await db.SaveChangesAsync();
        Assert.Equal(50m, m.Balance);

        MemberAccountService.PostPayment(db, m, 50m, "Card", "Manual", null, "", At(1));
        await db.SaveChangesAsync();

        Assert.Equal(0m, m.Balance);
        Assert.Null(m.OldestUnpaidChargeAt);
    }

    [Fact]
    public async Task PostPayment_reinstates_auto_suspended_member_when_balance_clears()
    {
        var (db, m) = await NewMemberAsync("reinstate", startingBalance: 100m);
        m.OldestUnpaidChargeAt = At(-90).ToString("o");
        m.SuspendedAt = At(-1).ToString("o");
        m.Status = "Suspended";
        m.Active = false;
        await db.SaveChangesAsync();

        MemberAccountService.PostPayment(db, m, 100m, "Cash", "Manual", null, "", At(0));
        await db.SaveChangesAsync();

        Assert.Equal(0m, m.Balance);
        Assert.Null(m.SuspendedAt);
        Assert.Equal("Active", m.Status);
        Assert.True(m.Active);
    }

    [Fact]
    public async Task PostPayment_rejects_unknown_method()
    {
        var (db, m) = await NewMemberAsync("bad-method");
        var res = MemberAccountService.PostPayment(
            db, m, 10m, "Bitcoin", "Manual", null, "", At(0));
        Assert.Equal("unknown_method", res.Error);
    }

    [Fact]
    public async Task VoidEntry_on_charge_posts_reversal_and_restores_balance()
    {
        var (db, m) = await NewMemberAsync("void-charge");

        var charge = MemberAccountService.PostCharge(
            db, m, 50m, "F&B", "Manual", null, "to be voided", At(0));
        await db.SaveChangesAsync();

        var voidRes = MemberAccountService.VoidEntry(db, charge.Entry!, m, "test void", At(1));
        await db.SaveChangesAsync();

        Assert.Null(voidRes.Error);
        Assert.Equal("Reversal", voidRes.Entry!.EntryType);
        Assert.Equal(charge.Entry!.Id, voidRes.Entry.ReversesEntryId);
        Assert.Equal(0m, m.Balance);
        Assert.Null(m.OldestUnpaidChargeAt);
        Assert.Equal(voidRes.Entry.PostedAt, charge.Entry.VoidedAt);
        Assert.Equal(voidRes.Entry.Id, charge.Entry.VoidedByEntryId);
    }

    [Fact]
    public async Task VoidEntry_on_payment_redebits_and_restamps_oldest_unpaid()
    {
        // Critical scenario from the plan:
        //   charge 100 at t0
        //   charge 50  at t1
        //   pay 100    at t2  -> balance = 50
        //   pay 50     at t3  -> balance = 0 (oldest cleared)
        //   void the 100 pay at t4
        //     -> balance back to 100
        //     -> oldest re-stamped to charge#2.PostedAt (the 50 charge),
        //        because the 50 payment covers charge#1 first chronologically.
        // The exact debt-cycle definition depends on walk order; what we
        // assert here is that the recomputed value is non-null and matches
        // ComputeOldestUnpaid against the persisted ledger.
        var (db, m) = await NewMemberAsync("void-pay-restamp");

        MemberAccountService.PostCharge(db, m, 100m, "F&B", "Manual", null, "c1", At(0));
        MemberAccountService.PostCharge(db, m, 50m, "Dues", "Manual", null, "c2", At(1));
        var pay100 = MemberAccountService.PostPayment(db, m, 100m, "Card", "Manual", null, "p100", At(2));
        MemberAccountService.PostPayment(db, m, 50m, "Card", "Manual", null, "p50", At(3));
        await db.SaveChangesAsync();
        Assert.Equal(0m, m.Balance);
        Assert.Null(m.OldestUnpaidChargeAt);

        MemberAccountService.VoidEntry(db, pay100.Entry!, m, "reverse 100 pay", At(4));
        await db.SaveChangesAsync();

        Assert.Equal(100m, m.Balance);
        Assert.NotNull(m.OldestUnpaidChargeAt);
        // Cache matches ledger-derived ground truth.
        Assert.Equal(
            MemberAccountService.ComputeOldestUnpaid(db, m.Id),
            m.OldestUnpaidChargeAt);
    }

    [Fact]
    public async Task VoidEntry_rejects_tab_sourced_entries()
    {
        var (db, m) = await NewMemberAsync("void-tab-source");

        var charge = MemberAccountService.PostCharge(
            db, m, 25m, "F&B", "Tab", "pay_fake", "from tab", At(0));
        await db.SaveChangesAsync();

        var voidRes = MemberAccountService.VoidEntry(db, charge.Entry!, m, "", At(1));
        Assert.Equal("void_via_tab", voidRes.Error);
        Assert.Null(charge.Entry!.VoidedAt);
    }

    [Fact]
    public async Task VoidEntry_rejects_already_voided_entries()
    {
        var (db, m) = await NewMemberAsync("double-void");

        var charge = MemberAccountService.PostCharge(
            db, m, 10m, "Adjustment", "Manual", null, "", At(0));
        await db.SaveChangesAsync();

        MemberAccountService.VoidEntry(db, charge.Entry!, m, "first void", At(1));
        await db.SaveChangesAsync();

        var secondVoid = MemberAccountService.VoidEntry(db, charge.Entry!, m, "second void", At(2));
        Assert.Equal("already_voided", secondVoid.Error);
    }

    [Fact]
    public async Task VoidEntry_rejects_reversal_entries()
    {
        var (db, m) = await NewMemberAsync("void-reversal");

        var charge = MemberAccountService.PostCharge(
            db, m, 10m, "Adjustment", "Manual", null, "", At(0));
        await db.SaveChangesAsync();

        var reversal = MemberAccountService.VoidEntry(db, charge.Entry!, m, "", At(1));
        await db.SaveChangesAsync();

        var voidOfReversal = MemberAccountService.VoidEntry(db, reversal.Entry!, m, "", At(2));
        Assert.Equal("cannot_void_reversal", voidOfReversal.Error);
    }

    [Fact]
    public async Task ComputeOldestUnpaid_matches_cache_after_mixed_operations()
    {
        // Defense-in-depth sanity test: after a sequence of charges and
        // payments, the cached field equals the from-ledger computation.
        var (db, m) = await NewMemberAsync("cache-sanity");

        MemberAccountService.PostCharge(db, m, 30m, "F&B", "Manual", null, "", At(0));
        MemberAccountService.PostCharge(db, m, 15m, "Dues", "Manual", null, "", At(1));
        MemberAccountService.PostPayment(db, m, 10m, "Card", "Manual", null, "", At(2));
        MemberAccountService.PostCharge(db, m, 5m, "Lesson", "Manual", null, "", At(3));
        await db.SaveChangesAsync();

        var computed = MemberAccountService.ComputeOldestUnpaid(db, m.Id);
        Assert.Equal(computed, m.OldestUnpaidChargeAt);
        Assert.True(m.Balance > 0m);
    }
}
