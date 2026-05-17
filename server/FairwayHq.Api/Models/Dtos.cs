namespace FairwayHq.Api.Models;

// DTOs intentionally mirror the TypeScript shape used by the client.
// Keep these as records with init-only props for safe deserialization.

public record MemberDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string Tier,
    double Handicap,
    string JoinDate,
    bool Active,
    decimal Balance,
    string Status,
    string? OldestUnpaidChargeAt,
    string? SuspendedAt,
    // Nullable so pre-v1 backup snapshots restore cleanly; Apply() coerces null → "".
    string? Notes
);

public record MemberApplicationDto(
    string Id,
    string FirstName,
    string LastName,
    string Email,
    string Phone,
    string RequestedTier,
    string? SponsoringMemberId,
    decimal InitiationFee,
    string Notes,
    string Status,
    string SubmittedAt,
    string? ReviewedAt,
    string? ReviewedBy,
    string? ReviewNote,
    string? ActivatedMemberId
);

public record ApplicationReviewDto(string? Reviewer, string? Note);

// Aggregated view for the member detail / CRM page. Only Completed tee times
// contribute to LifetimeRounds and LastPlayedDate; RecentRounds is also
// restricted to Completed so the table reads as a true playing history.
public record MemberOverviewDto(
    MemberDto Member,
    string? LastPlayedDate,
    int LifetimeRounds,
    List<TeeTimeDto> RecentRounds
);

public record DunningRunResultDto(int Suspended, int Reinstated, string[] AffectedMemberIds);

public record CourseDto(
    string Id,
    string Name,
    int Holes,
    int Par,
    int Yardage,
    double Rating,
    int Slope,
    string Status,
    string OpenTime,
    string CloseTime,
    string Notes
);

public record TeeTimeDto(
    string Id,
    string Date,
    string Time,
    string CourseId,
    List<string> Players,
    bool Cart,
    string Status,
    string Notes
);

public record StaffMemberDto(
    string Id,
    string FirstName,
    string LastName,
    string Role,
    string Email,
    string Phone,
    decimal HourlyRate,
    bool Active
);

public record ShiftDto(
    string Id,
    string StaffId,
    string Date,
    string Start,
    string End,
    string Notes
);

public record WeeklyTemplateDto(
    string Id,
    string StaffId,
    int DayOfWeek,
    string Start,
    string End,
    string Notes
);

public record ProductDto(
    string Id,
    string Name,
    string Category,
    string Sku,
    decimal Price,
    decimal Cost,
    int Stock,
    int ReorderLevel
);

public record TournamentDto(
    string Id,
    string Name,
    string Date,
    string Format,
    string CourseId,
    decimal EntryFee,
    int MaxPlayers,
    List<string> Registered,
    string Status
);

public record MaintenanceTaskDto(
    string Id,
    string Title,
    string Category,
    string CourseId,
    string AssignedTo,
    string DueDate,
    string Priority,
    string Status,
    string Notes
);

public record TabLineItemDto(
    string Id,
    string ProductId,
    string Name,
    decimal UnitPrice,
    int Quantity,
    string Notes,
    string AddedAt
);

public record TabPaymentDto(
    string Id,
    string Method,
    decimal Amount,
    string? PayerMemberId,
    string Note,
    string PaidAt
);

public record PlayerTabDto(
    string Id,
    string OpenedAt,
    string? ClosedAt,
    string Status,
    List<string> MemberIds,
    List<string> Guests,
    string? TeeTimeId,
    List<TabLineItemDto> Items,
    List<TabPaymentDto> Payments,
    decimal TipAmount,
    decimal TaxRate,
    string Notes
);

// Bulk snapshot for backup/restore + the "load everything" client bootstrap.
public record DataSnapshot(
    List<MemberDto> Members,
    List<CourseDto> Courses,
    List<TeeTimeDto> TeeTimes,
    List<StaffMemberDto> Staff,
    List<ShiftDto> Shifts,
    List<WeeklyTemplateDto> WeeklyTemplates,
    List<ProductDto> Products,
    List<TournamentDto> Tournaments,
    List<MaintenanceTaskDto> Maintenance,
    List<PlayerTabDto> Tabs,
    List<MemberApplicationDto> MemberApplications
);

// Body for tab payment posting (auto-stamped PaidAt server-side).
public record CreatePaymentDto(
    string Method,
    decimal Amount,
    string? PayerMemberId,
    string Note
);

// Body for adding a tab item (server snapshots product price + decrements stock).
public record CreateLineItemDto(
    string ProductId,
    int Quantity,
    string Notes
);
