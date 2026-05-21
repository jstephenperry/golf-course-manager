namespace FairwayHq.Api.Authorization;

/// <summary>
/// The canonical catalog of permission strings. Every <c>RequireAuthorization</c>
/// in an endpoint references one of these constants — never a raw string —
/// so a typo at the endpoint is a compile error, and a global rename is
/// a refactor instead of a grep-and-pray.
///
/// Permissions are namespaced as <c>resource:action</c>. They live ONLY in
/// the server (and a duplicated client-side copy for UX). Keycloak knows
/// about <see cref="Roles"/>; the role→permission expansion happens in
/// <see cref="RolePermissions"/>.
///
/// See docs/decisions/0003-auth-rbac.md for the full catalog and matrix.
/// </summary>
public static class Permissions
{
    // ----- Tee times -----
    public const string TeeTimesRead = "tee-times:read";
    public const string TeeTimesWrite = "tee-times:write";
    public const string TeeTimesCheckin = "tee-times:checkin";
    public const string TeeTimesCancel = "tee-times:cancel";

    // ----- Members -----
    public const string MembersRead = "members:read";
    public const string MembersWrite = "members:write";
    public const string MembersSuspend = "members:suspend";
    public const string MembersApplicationsRead = "members:applications:read";
    public const string MembersApplicationsWrite = "members:applications:write";
    public const string MembersOverviewRead = "members:overview:read";

    // ----- Member ledger / accounting -----
    public const string LedgerRead = "ledger:read";
    public const string LedgerCharge = "ledger:charge";
    public const string LedgerPayment = "ledger:payment";
    public const string LedgerVoid = "ledger:void";
    public const string DunningRun = "dunning:run";

    // ----- Courses / nines -----
    public const string CoursesRead = "courses:read";
    public const string CoursesWrite = "courses:write";
    public const string NinesRead = "nines:read";
    public const string NinesWrite = "nines:write";

    // ----- Staff / scheduling -----
    public const string StaffRead = "staff:read";
    public const string StaffWrite = "staff:write";
    public const string ShiftsRead = "shifts:read";
    public const string ShiftsWrite = "shifts:write";
    public const string TemplatesRead = "templates:read";
    public const string TemplatesWrite = "templates:write";

    // ----- Pro shop -----
    public const string ProductsRead = "products:read";
    public const string ProductsWrite = "products:write";
    public const string ProductsStock = "products:stock";

    // ----- Tournaments -----
    public const string TournamentsRead = "tournaments:read";
    public const string TournamentsWrite = "tournaments:write";

    // ----- Maintenance -----
    public const string MaintenanceRead = "maintenance:read";
    public const string MaintenanceWrite = "maintenance:write";

    // ----- Tabs (POS) -----
    public const string TabsRead = "tabs:read";
    public const string TabsWrite = "tabs:write";
    public const string TabsPayment = "tabs:payment";
    public const string TabsSettle = "tabs:settle";
    public const string TabsVoid = "tabs:void";

    // ----- System ops -----
    public const string ImportRun = "import:run";
    public const string SnapshotExport = "snapshot:export";
    public const string SnapshotRestore = "snapshot:restore";
    public const string SystemClear = "system:clear";
    public const string SystemHealthRead = "system:health:read";

    // ----- Remote admin (ADR 0002) -----
    public const string AdminRead = "admin:read";
    public const string AdminWrite = "admin:write";
    public const string AdminHost = "admin:host";

    /// <summary>
    /// Every permission constant defined above, useful for tests + the
    /// role-mapping sanity check.
    /// </summary>
    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        TeeTimesRead, TeeTimesWrite, TeeTimesCheckin, TeeTimesCancel,
        MembersRead, MembersWrite, MembersSuspend,
        MembersApplicationsRead, MembersApplicationsWrite, MembersOverviewRead,
        LedgerRead, LedgerCharge, LedgerPayment, LedgerVoid, DunningRun,
        CoursesRead, CoursesWrite, NinesRead, NinesWrite,
        StaffRead, StaffWrite, ShiftsRead, ShiftsWrite, TemplatesRead, TemplatesWrite,
        ProductsRead, ProductsWrite, ProductsStock,
        TournamentsRead, TournamentsWrite,
        MaintenanceRead, MaintenanceWrite,
        TabsRead, TabsWrite, TabsPayment, TabsSettle, TabsVoid,
        ImportRun, SnapshotExport, SnapshotRestore, SystemClear, SystemHealthRead,
        AdminRead, AdminWrite, AdminHost,
    };
}
