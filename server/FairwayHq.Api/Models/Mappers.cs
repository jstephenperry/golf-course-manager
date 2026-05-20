using System.Text.Json;

namespace FairwayHq.Api.Models;

public static class Mappers
{
    private static readonly JsonSerializerOptions Json = new();

    private static List<string> ParseList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json, Json) ?? new List<string>(); }
        catch { return new List<string>(); }
    }

    private static string SerializeList(IEnumerable<string> items) =>
        JsonSerializer.Serialize(items.ToList(), Json);

    public static MemberDto ToDto(this Member m) =>
        new(m.Id, m.FirstName, m.LastName, m.Email, m.Phone, m.Tier,
            m.Handicap, m.JoinDate, m.Active, m.Balance,
            string.IsNullOrEmpty(m.Status) ? (m.Active ? "Active" : "Inactive") : m.Status,
            m.OldestUnpaidChargeAt, m.SuspendedAt, m.Notes);

    public static void Apply(this Member m, MemberDto d)
    {
        m.FirstName = d.FirstName; m.LastName = d.LastName;
        m.Email = d.Email; m.Phone = d.Phone; m.Tier = d.Tier;
        m.Handicap = d.Handicap; m.JoinDate = d.JoinDate;
        // Prefer status from the DTO; fall back to legacy Active.
        var status = string.IsNullOrEmpty(d.Status)
            ? (d.Active ? "Active" : "Inactive")
            : d.Status;
        m.Status = status;
        m.Active = status == "Active";
        m.Balance = d.Balance;
        m.OldestUnpaidChargeAt = d.OldestUnpaidChargeAt;
        m.SuspendedAt = d.SuspendedAt;
        m.Notes = d.Notes ?? string.Empty;
    }

    // A3: Profile-only update. Never touches Balance, Status, Active, or the
    // aging/suspension timestamps — those are owned by the ledger and
    // suspend/reinstate flows.
    public static void ApplyProfile(this Member m, MemberUpdateDto d)
    {
        m.FirstName = d.FirstName; m.LastName = d.LastName;
        m.Email = d.Email; m.Phone = d.Phone; m.Tier = d.Tier;
        m.Handicap = d.Handicap; m.JoinDate = d.JoinDate;
        m.Notes = d.Notes ?? string.Empty;
    }

    public static MemberApplicationDto ToDto(this MemberApplication a) =>
        new(a.Id, a.FirstName, a.LastName, a.Email, a.Phone, a.RequestedTier,
            a.SponsoringMemberId, a.InitiationFee, a.Notes, a.Status,
            a.SubmittedAt, a.ReviewedAt, a.ReviewedBy, a.ReviewNote, a.ActivatedMemberId);

    public static void Apply(this MemberApplication a, MemberApplicationDto d)
    {
        a.FirstName = d.FirstName; a.LastName = d.LastName;
        a.Email = d.Email; a.Phone = d.Phone;
        a.RequestedTier = d.RequestedTier;
        a.SponsoringMemberId = d.SponsoringMemberId;
        a.InitiationFee = d.InitiationFee;
        a.Notes = d.Notes;
        // Status / lifecycle fields are managed by dedicated endpoints,
        // not by raw PUT; we still preserve them here for restore use.
        if (!string.IsNullOrEmpty(d.Status)) a.Status = d.Status;
        if (!string.IsNullOrEmpty(d.SubmittedAt)) a.SubmittedAt = d.SubmittedAt;
        a.ReviewedAt = d.ReviewedAt; a.ReviewedBy = d.ReviewedBy;
        a.ReviewNote = d.ReviewNote; a.ActivatedMemberId = d.ActivatedMemberId;
    }

    public static CourseDto ToDto(this Course c) =>
        new(c.Id, c.Name, c.FrontNineId, c.BackNineId, c.Rating, c.Slope,
            c.Status, c.OpenTime, c.CloseTime, c.Notes);

    public static void Apply(this Course c, CourseDto d)
    {
        c.Name = d.Name;
        c.FrontNineId = string.IsNullOrWhiteSpace(d.FrontNineId) ? null : d.FrontNineId;
        c.BackNineId = string.IsNullOrWhiteSpace(d.BackNineId) ? null : d.BackNineId;
        c.Rating = d.Rating; c.Slope = d.Slope; c.Status = d.Status;
        c.OpenTime = d.OpenTime; c.CloseTime = d.CloseTime; c.Notes = d.Notes;
    }

    public static NineTeeSetDto ToDto(this NineTeeSet t) =>
        new(t.Id, t.NineId, t.Name, t.Color, t.SortOrder);

    public static HoleYardageDto ToDto(this HoleYardage y) =>
        new(y.Id, y.HoleId, y.TeeSetId, y.Yards);

    public static HoleDto ToDto(this Hole h) =>
        new(h.Id, h.NineId, h.Number, h.Par, h.HandicapIndex, h.Notes,
            h.Yardages.OrderBy(y => y.TeeSetId).Select(y => y.ToDto()).ToList());

    public static NineDto ToDto(this Nine n) =>
        new(n.Id, n.Name, n.Description, n.Notes,
            n.TeeSets.OrderBy(t => t.SortOrder).ThenBy(t => t.Name).Select(t => t.ToDto()).ToList(),
            n.Holes.OrderBy(h => h.Number).Select(h => h.ToDto()).ToList());

    public static TeeTimeDto ToDto(this TeeTime t) =>
        new(t.Id, t.Date, t.Time, t.CourseId, ParseList(t.PlayersJson),
            t.Cart, t.Status, t.Notes);

    public static void Apply(this TeeTime t, TeeTimeDto d)
    {
        t.Date = d.Date; t.Time = d.Time; t.CourseId = d.CourseId;
        t.PlayersJson = SerializeList(d.Players); t.Cart = d.Cart;
        t.Status = d.Status; t.Notes = d.Notes;
    }

    public static StaffMemberDto ToDto(this StaffMember s) =>
        new(s.Id, s.FirstName, s.LastName, s.Role, s.Email, s.Phone,
            s.HourlyRate, s.Active);

    public static void Apply(this StaffMember s, StaffMemberDto d)
    {
        s.FirstName = d.FirstName; s.LastName = d.LastName; s.Role = d.Role;
        s.Email = d.Email; s.Phone = d.Phone; s.HourlyRate = d.HourlyRate;
        s.Active = d.Active;
    }

    public static ShiftDto ToDto(this Shift s) =>
        new(s.Id, s.StaffId, s.Date, s.Start, s.End, s.Notes);

    public static void Apply(this Shift s, ShiftDto d)
    {
        s.StaffId = d.StaffId; s.Date = d.Date;
        s.Start = d.Start; s.End = d.End; s.Notes = d.Notes;
    }

    public static WeeklyTemplateDto ToDto(this WeeklyTemplate w) =>
        new(w.Id, w.StaffId, w.DayOfWeek, w.Start, w.End, w.Notes);

    public static void Apply(this WeeklyTemplate w, WeeklyTemplateDto d)
    {
        w.StaffId = d.StaffId; w.DayOfWeek = d.DayOfWeek;
        w.Start = d.Start; w.End = d.End; w.Notes = d.Notes;
    }

    public static ProductDto ToDto(this Product p) =>
        new(p.Id, p.Name, p.Category, p.Sku, p.Price, p.Cost,
            p.Stock, p.ReorderLevel);

    public static void Apply(this Product p, ProductDto d)
    {
        p.Name = d.Name; p.Category = d.Category; p.Sku = d.Sku;
        p.Price = d.Price; p.Cost = d.Cost; p.Stock = d.Stock;
        p.ReorderLevel = d.ReorderLevel;
    }

    public static TournamentDto ToDto(this Tournament t) =>
        new(t.Id, t.Name, t.Date, t.Format, t.CourseId, t.EntryFee,
            t.MaxPlayers, ParseList(t.RegisteredJson), t.Status);

    public static void Apply(this Tournament t, TournamentDto d)
    {
        t.Name = d.Name; t.Date = d.Date; t.Format = d.Format;
        t.CourseId = d.CourseId; t.EntryFee = d.EntryFee;
        t.MaxPlayers = d.MaxPlayers; t.Status = d.Status;
        t.RegisteredJson = SerializeList(d.Registered);
    }

    public static MaintenanceTaskDto ToDto(this MaintenanceTask m) =>
        new(m.Id, m.Title, m.Category, m.CourseId, m.AssignedTo,
            m.DueDate, m.Priority, m.Status, m.Notes);

    public static void Apply(this MaintenanceTask m, MaintenanceTaskDto d)
    {
        m.Title = d.Title; m.Category = d.Category; m.CourseId = d.CourseId;
        m.AssignedTo = d.AssignedTo; m.DueDate = d.DueDate;
        m.Priority = d.Priority; m.Status = d.Status; m.Notes = d.Notes;
    }

    public static TabLineItemDto ToDto(this TabLineItem li) =>
        new(li.Id, li.ProductId, li.Name, li.UnitPrice, li.Quantity,
            li.Notes, li.AddedAt);

    public static TabPaymentDto ToDto(this TabPayment p) =>
        new(p.Id, p.Method, p.Amount, p.PayerMemberId, p.Note, p.PaidAt);

    public static PlayerTabDto ToDto(this PlayerTab t) =>
        new(t.Id, t.OpenedAt, t.ClosedAt, t.Status,
            ParseList(t.MemberIdsJson), ParseList(t.GuestsJson),
            t.TeeTimeId,
            t.Items.OrderBy(i => i.AddedAt).Select(i => i.ToDto()).ToList(),
            t.Payments.OrderBy(p => p.PaidAt).Select(p => p.ToDto()).ToList(),
            t.TipAmount, t.TaxRate, t.Notes);

    public static void ApplyMeta(this PlayerTab t, PlayerTabDto d)
    {
        t.OpenedAt = d.OpenedAt; t.ClosedAt = d.ClosedAt;
        t.Status = d.Status;
        t.MemberIdsJson = SerializeList(d.MemberIds);
        t.GuestsJson = SerializeList(d.Guests);
        t.TeeTimeId = d.TeeTimeId;
        t.TipAmount = d.TipAmount; t.TaxRate = d.TaxRate;
        t.Notes = d.Notes;
    }

    public static MemberLedgerEntryDto ToDto(this MemberLedgerEntry e) =>
        new(e.Id, e.MemberId, e.EntryType, e.Category, e.Amount, e.Method,
            e.Note, e.PostedAt, e.SourceKind, e.SourceId,
            e.ReversesEntryId, e.VoidedAt, e.VoidedByEntryId);

    // Ledger entries are append-only — there's no Apply() because they're
    // never mutated post-write. Restore constructs entities directly from
    // DTOs in OpsEndpoints.
}
