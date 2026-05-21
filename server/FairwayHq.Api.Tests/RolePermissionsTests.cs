using FairwayHq.Api.Authorization;

namespace FairwayHq.Api.Tests;

/// <summary>
/// Unit tests for the role → permission matrix. These don't need an
/// HTTP factory; they verify the static lookup directly. Catching a
/// matrix mistake here is much cheaper than chasing it through 50+
/// endpoint policy bindings later.
/// </summary>
public class RolePermissionsTests
{
    [Fact]
    public void Every_role_has_a_permission_set()
    {
        foreach (var role in Roles.All)
        {
            Assert.True(
                RolePermissions.Map.ContainsKey(role),
                $"Role '{role}' is declared but has no permissions defined");
        }
    }

    [Fact]
    public void Every_listed_permission_in_the_matrix_is_a_known_permission()
    {
        // Defends against typos like "tee-times:checkin" vs "tee-time:checkin".
        foreach (var (role, perms) in RolePermissions.Map)
        {
            foreach (var p in perms)
            {
                Assert.True(
                    Permissions.All.Contains(p),
                    $"Role '{role}' grants unknown permission '{p}'");
            }
        }
    }

    [Fact]
    public void Owner_has_every_permission()
    {
        var ownerPerms = RolePermissions.Map[Roles.Owner];
        foreach (var p in Permissions.All)
        {
            Assert.True(ownerPerms.Contains(p),
                $"Owner is missing permission '{p}'");
        }
    }

    [Fact]
    public void Greenkeeper_cannot_touch_inventory_or_schedules()
    {
        // The goal-statement example: a greenkeeper cannot manage
        // inventory, edit/set individual or weekly schedules, or touch
        // schedule templates.
        var perms = RolePermissions.Map[Roles.Greenkeeper];

        // Inventory
        Assert.DoesNotContain(Permissions.ProductsRead,   perms);
        Assert.DoesNotContain(Permissions.ProductsWrite,  perms);
        Assert.DoesNotContain(Permissions.ProductsStock,  perms);

        // Individual schedules (shifts)
        Assert.DoesNotContain(Permissions.ShiftsRead,   perms);
        Assert.DoesNotContain(Permissions.ShiftsWrite,  perms);

        // Weekly schedule templates
        Assert.DoesNotContain(Permissions.TemplatesRead,  perms);
        Assert.DoesNotContain(Permissions.TemplatesWrite, perms);

        // Sanity: greenkeeper CAN still do their job — maintenance + read
        // tee times so they know what's busy on the course.
        Assert.Contains(Permissions.MaintenanceRead,   perms);
        Assert.Contains(Permissions.MaintenanceWrite,  perms);
        Assert.Contains(Permissions.TeeTimesRead,      perms);
    }

    [Fact]
    public void Destructive_system_ops_are_owner_only()
    {
        foreach (var perm in new[]
                 {
                     Permissions.ImportRun,
                     Permissions.SnapshotRestore,
                     Permissions.SystemClear,
                     Permissions.AdminHost,
                     Permissions.AdminWrite,
                 })
        {
            foreach (var (role, perms) in RolePermissions.Map)
            {
                if (role == Roles.Owner) continue;
                Assert.False(perms.Contains(perm),
                    $"Role '{role}' should NOT have '{perm}' (owner-only)");
            }
        }
    }

    [Fact]
    public void PermissionsFor_unions_across_multiple_roles()
    {
        // A user that's both greenkeeper AND pro-shop gets the union.
        var combined = RolePermissions.PermissionsFor(
            new[] { Roles.Greenkeeper, Roles.ProShop });

        Assert.Contains(Permissions.MaintenanceWrite, combined); // from greenkeeper
        Assert.Contains(Permissions.TabsPayment,      combined); // from pro-shop
        Assert.Contains(Permissions.TeeTimesCheckin,  combined); // both
    }

    [Fact]
    public void PermissionsFor_ignores_unknown_roles_silently()
    {
        // Forward-compat: if Keycloak adds a new role before the app
        // catches up, we don't crash — we just grant nothing for it.
        var perms = RolePermissions.PermissionsFor(
            new[] { "future-role-that-doesnt-exist-yet" });

        Assert.Empty(perms);
    }

    [Fact]
    public void Starter_role_is_truly_minimal()
    {
        // The kiosk-near-the-first-tee role. Should only have read +
        // check-in for tee times — nothing else. Belt-and-suspenders
        // against accidental scope creep.
        var perms = RolePermissions.Map[Roles.Starter];
        Assert.Equal(2, perms.Count);
        Assert.Contains(Permissions.TeeTimesRead, perms);
        Assert.Contains(Permissions.TeeTimesCheckin, perms);
    }
}
