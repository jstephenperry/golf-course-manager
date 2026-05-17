using System.ComponentModel.DataAnnotations;

namespace FairwayHq.Api.Models;

public class Member
{
    [Key] public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Tier { get; set; } = "Full";
    public double Handicap { get; set; }
    public string JoinDate { get; set; } = string.Empty;

    // Status supersedes the bool. Kept in sync via Member.Active so older
    // clients that read the bool continue to work; new clients should read
    // Status. Allowed values: "Active", "Suspended", "Inactive".
    public string Status { get; set; } = "Active";
    public bool Active { get; set; } = true;

    public decimal Balance { get; set; }

    // Aging: the moment the member's outstanding balance first went above
    // zero in the current debt cycle. Cleared as soon as balance returns
    // to zero. Used by the dunning service for NET-X past-due suspension.
    public string? OldestUnpaidChargeAt { get; set; }

    // Set when the dunning service auto-suspends. Cleared on reinstate.
    public string? SuspendedAt { get; set; }
}

public class Course
{
    [Key] public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Holes { get; set; }
    public int Par { get; set; }
    public int Yardage { get; set; }
    public double Rating { get; set; }
    public int Slope { get; set; }
    public string Status { get; set; } = "Open";
    public string OpenTime { get; set; } = "06:00";
    public string CloseTime { get; set; } = "18:00";
    public string Notes { get; set; } = string.Empty;
}

public class TeeTime
{
    [Key] public string Id { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string CourseId { get; set; } = string.Empty;
    public string PlayersJson { get; set; } = "[]";
    public bool Cart { get; set; }
    public string Status { get; set; } = "Booked";
    public string Notes { get; set; } = string.Empty;
}

public class StaffMember
{
    [Key] public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public bool Active { get; set; } = true;
}

public class Shift
{
    [Key] public string Id { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class WeeklyTemplate
{
    [Key] public string Id { get; set; } = string.Empty;
    public string StaffId { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class Product
{
    [Key] public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = "Accessories";
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Cost { get; set; }
    public int Stock { get; set; }
    public int ReorderLevel { get; set; }
}

public class Tournament
{
    [Key] public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Format { get; set; } = "Stroke Play";
    public string CourseId { get; set; } = string.Empty;
    public decimal EntryFee { get; set; }
    public int MaxPlayers { get; set; }
    public string RegisteredJson { get; set; } = "[]";
    public string Status { get; set; } = "Scheduled";
}

public class MaintenanceTask
{
    [Key] public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "Other";
    public string CourseId { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string Priority { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public string Notes { get; set; } = string.Empty;
}

public class PlayerTab
{
    [Key] public string Id { get; set; } = string.Empty;
    public string OpenedAt { get; set; } = string.Empty;
    public string? ClosedAt { get; set; }
    public string Status { get; set; } = "Open";
    public string MemberIdsJson { get; set; } = "[]";
    public string GuestsJson { get; set; } = "[]";
    public string? TeeTimeId { get; set; }
    public decimal TipAmount { get; set; }
    public decimal TaxRate { get; set; }
    public string Notes { get; set; } = string.Empty;

    public List<TabLineItem> Items { get; set; } = new();
    public List<TabPayment> Payments { get; set; } = new();
}

public class TabLineItem
{
    [Key] public string Id { get; set; } = string.Empty;
    public string TabId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string AddedAt { get; set; } = string.Empty;
}

public class TabPayment
{
    [Key] public string Id { get; set; } = string.Empty;
    public string TabId { get; set; } = string.Empty;
    public string Method { get; set; } = "Card";
    public decimal Amount { get; set; }
    public string? PayerMemberId { get; set; }
    public string Note { get; set; } = string.Empty;
    public string PaidAt { get; set; } = string.Empty;
}

public class MemberApplication
{
    [Key] public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string RequestedTier { get; set; } = "Full";
    public string? SponsoringMemberId { get; set; }
    public decimal InitiationFee { get; set; }
    public string Notes { get; set; } = string.Empty;

    // Pending → Approved → Activated (creates Member);
    // Pending → Rejected; either path can be Withdrawn.
    public string Status { get; set; } = "Pending";
    public string SubmittedAt { get; set; } = string.Empty;
    public string? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? ReviewNote { get; set; }
    public string? ActivatedMemberId { get; set; }
}
