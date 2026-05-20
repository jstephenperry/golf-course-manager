using static FairwayHq.Api.Authorization.Permissions;

namespace FairwayHq.Api.Authorization;

/// <summary>
/// The canonical role → permissions matrix. This is the source of truth
/// the <see cref="PermissionClaimsTransformation"/> uses to expand a
/// user's role claims into permission claims.
///
/// Keep in sync with the matrix in docs/decisions/0003-auth-rbac.md.
/// </summary>
public static class RolePermissions
{
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> Map =
        new Dictionary<string, IReadOnlySet<string>>
        {
            [Roles.Owner] = new HashSet<string>(Permissions.All),

            [Roles.Manager] = new HashSet<string>
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
                SnapshotExport,
                SystemHealthRead,
                AdminRead,
                // Excludes: import:run, snapshot:restore, system:clear,
                // admin:write, admin:host. Destructive / system-owner only.
            },

            [Roles.Pro] = new HashSet<string>
            {
                TeeTimesRead, TeeTimesWrite, TeeTimesCheckin, TeeTimesCancel,
                MembersRead, MembersWrite,
                MembersApplicationsRead,
                MembersOverviewRead,
                LedgerRead,
                CoursesRead,
                NinesRead,
                ProductsRead, ProductsStock,
                TournamentsRead, TournamentsWrite,
                MaintenanceRead,
                TabsRead, TabsWrite, TabsPayment, TabsSettle,
            },

            [Roles.AssistantPro] = new HashSet<string>
            {
                TeeTimesRead, TeeTimesWrite, TeeTimesCheckin,
                MembersRead,
                MembersOverviewRead,
                CoursesRead,
                NinesRead,
                ProductsRead,
                TournamentsRead,
                TabsRead, TabsWrite, TabsPayment, TabsSettle,
            },

            [Roles.ProShop] = new HashSet<string>
            {
                TeeTimesRead, TeeTimesCheckin,
                MembersRead,
                MembersOverviewRead,
                LedgerPayment,
                CoursesRead,
                ProductsRead, ProductsStock,
                TabsRead, TabsWrite, TabsPayment, TabsSettle,
            },

            [Roles.Greenkeeper] = new HashSet<string>
            {
                TeeTimesRead,
                CoursesRead,
                NinesRead,
                MaintenanceRead, MaintenanceWrite,
            },

            [Roles.Starter] = new HashSet<string>
            {
                TeeTimesRead, TeeTimesCheckin,
            },
        };

    /// <summary>
    /// Expand a set of role names into the union of their permissions.
    /// Unknown role names are silently ignored — the security boundary
    /// is "user gets only what mapped roles grant," not "block on
    /// unrecognized role." Keeps the app forward-compatible if Keycloak
    /// introduces additional roles before the app catches up.
    /// </summary>
    public static IReadOnlySet<string> PermissionsFor(IEnumerable<string> roles)
    {
        var result = new HashSet<string>();
        foreach (var role in roles)
        {
            if (Map.TryGetValue(role, out var perms))
            {
                result.UnionWith(perms);
            }
        }
        return result;
    }
}
