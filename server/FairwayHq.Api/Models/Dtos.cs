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
    int NoShowCount,
    List<TeeTimeDto> RecentRounds
);

// One immutable row of the member ledger. See MemberLedgerEntry entity
// for field semantics.
public record MemberLedgerEntryDto(
    string Id,
    string MemberId,
    string EntryType,
    string Category,
    decimal Amount,
    string? Method,
    string Note,
    string PostedAt,
    string SourceKind,
    string? SourceId,
    string? ReversesEntryId,
    string? VoidedAt,
    string? VoidedByEntryId
);

public record MemberLedgerListDto(
    List<MemberLedgerEntryDto> Entries,
    bool HasMore
);

// Bodies for the manual posting endpoints.
public record CreateManualChargeDto(decimal Amount, string Category, string Note);
public record CreateManualPaymentDto(decimal Amount, string Method, string Note);
public record VoidLedgerEntryDto(string Note);

public record DunningRunResultDto(int Suspended, int Reinstated, string[] AffectedMemberIds);

// Courses are an assembled round of one or two Nines. Holes/Par/Yardage
// are derived from the referenced Nines on the client and are not part
// of the DTO.
public record CourseDto(
    string Id,
    string Name,
    string? FrontNineId,
    string? BackNineId,
    double Rating,
    int Slope,
    string Status,
    string OpenTime,
    string CloseTime,
    string Notes
);

public record HoleYardageDto(
    string Id,
    string HoleId,
    string TeeSetId,
    int Yards
);

public record HoleDto(
    string Id,
    string NineId,
    int Number,
    int Par,
    int HandicapIndex,
    string Notes,
    List<HoleYardageDto> Yardages
);

public record NineTeeSetDto(
    string Id,
    string NineId,
    string Name,
    string Color,
    int SortOrder
);

public record NineDto(
    string Id,
    string Name,
    string Description,
    string Notes,
    List<NineTeeSetDto> TeeSets,
    List<HoleDto> Holes
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
// LedgerEntries is nullable so pre-ledger backups restore cleanly — the
// ops restore handler coerces null → empty list.
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
    List<MemberApplicationDto> MemberApplications,
    List<MemberLedgerEntryDto>? LedgerEntries = null,
    // Nullable so pre-v2 backup snapshots restore cleanly; restore handler
    // coerces null → empty list. The Nines own their tee sets, holes, and
    // hole yardages — those are nested inside NineDto and don't need
    // top-level snapshot lists.
    List<NineDto>? Nines = null
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
