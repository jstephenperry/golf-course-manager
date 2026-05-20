// Mirrors server/FairwayHq.Api/Authorization/*.cs. Keep in sync.
//
// The canonical role → permissions matrix duplicated from the server's
// `RolePermissions.cs`. The server is the security boundary; this copy
// drives UX-level role-aware rendering only. Drift is allowed; security
// is preserved as long as the server's matrix stays canonical.

import {
  ADMIN_READ,
  ALL_PERMISSIONS,
  COURSES_READ,
  COURSES_WRITE,
  DUNNING_RUN,
  LEDGER_CHARGE,
  LEDGER_PAYMENT,
  LEDGER_READ,
  LEDGER_VOID,
  MAINTENANCE_READ,
  MAINTENANCE_WRITE,
  MEMBERS_APPLICATIONS_READ,
  MEMBERS_APPLICATIONS_WRITE,
  MEMBERS_OVERVIEW_READ,
  MEMBERS_READ,
  MEMBERS_SUSPEND,
  MEMBERS_WRITE,
  NINES_READ,
  NINES_WRITE,
  PRODUCTS_READ,
  PRODUCTS_STOCK,
  PRODUCTS_WRITE,
  SHIFTS_READ,
  SHIFTS_WRITE,
  SNAPSHOT_EXPORT,
  STAFF_READ,
  STAFF_WRITE,
  SYSTEM_HEALTH_READ,
  TABS_PAYMENT,
  TABS_READ,
  TABS_SETTLE,
  TABS_VOID,
  TABS_WRITE,
  TEE_TIMES_CANCEL,
  TEE_TIMES_CHECKIN,
  TEE_TIMES_READ,
  TEE_TIMES_WRITE,
  TEMPLATES_READ,
  TEMPLATES_WRITE,
  TOURNAMENTS_READ,
  TOURNAMENTS_WRITE,
} from "./permissions";
import {
  ASSISTANT_PRO,
  GREENKEEPER,
  MANAGER,
  OWNER,
  PRO,
  PRO_SHOP,
  STARTER,
} from "./roles";

/**
 * Role → permissions map. Exact translation of server `RolePermissions.Map`.
 * Owner gets every permission; everyone else is an explicit allow-list.
 */
export const ROLE_PERMISSIONS: ReadonlyMap<string, ReadonlySet<string>> =
  new Map<string, ReadonlySet<string>>([
    [OWNER, new Set<string>(ALL_PERMISSIONS)],

    [
      MANAGER,
      new Set<string>([
        TEE_TIMES_READ,
        TEE_TIMES_WRITE,
        TEE_TIMES_CHECKIN,
        TEE_TIMES_CANCEL,
        MEMBERS_READ,
        MEMBERS_WRITE,
        MEMBERS_SUSPEND,
        MEMBERS_APPLICATIONS_READ,
        MEMBERS_APPLICATIONS_WRITE,
        MEMBERS_OVERVIEW_READ,
        LEDGER_READ,
        LEDGER_CHARGE,
        LEDGER_PAYMENT,
        LEDGER_VOID,
        DUNNING_RUN,
        COURSES_READ,
        COURSES_WRITE,
        NINES_READ,
        NINES_WRITE,
        STAFF_READ,
        STAFF_WRITE,
        SHIFTS_READ,
        SHIFTS_WRITE,
        TEMPLATES_READ,
        TEMPLATES_WRITE,
        PRODUCTS_READ,
        PRODUCTS_WRITE,
        PRODUCTS_STOCK,
        TOURNAMENTS_READ,
        TOURNAMENTS_WRITE,
        MAINTENANCE_READ,
        MAINTENANCE_WRITE,
        TABS_READ,
        TABS_WRITE,
        TABS_PAYMENT,
        TABS_SETTLE,
        TABS_VOID,
        SNAPSHOT_EXPORT,
        SYSTEM_HEALTH_READ,
        ADMIN_READ,
        // Excludes: import:run, snapshot:restore, system:clear,
        // admin:write, admin:host. Destructive / system-owner only.
      ]),
    ],

    [
      PRO,
      new Set<string>([
        TEE_TIMES_READ,
        TEE_TIMES_WRITE,
        TEE_TIMES_CHECKIN,
        TEE_TIMES_CANCEL,
        MEMBERS_READ,
        MEMBERS_WRITE,
        MEMBERS_APPLICATIONS_READ,
        MEMBERS_OVERVIEW_READ,
        LEDGER_READ,
        COURSES_READ,
        NINES_READ,
        PRODUCTS_READ,
        PRODUCTS_STOCK,
        TOURNAMENTS_READ,
        TOURNAMENTS_WRITE,
        MAINTENANCE_READ,
        TABS_READ,
        TABS_WRITE,
        TABS_PAYMENT,
        TABS_SETTLE,
      ]),
    ],

    [
      ASSISTANT_PRO,
      new Set<string>([
        TEE_TIMES_READ,
        TEE_TIMES_WRITE,
        TEE_TIMES_CHECKIN,
        MEMBERS_READ,
        MEMBERS_OVERVIEW_READ,
        COURSES_READ,
        NINES_READ,
        PRODUCTS_READ,
        TOURNAMENTS_READ,
        TABS_READ,
        TABS_WRITE,
        TABS_PAYMENT,
        TABS_SETTLE,
      ]),
    ],

    [
      PRO_SHOP,
      new Set<string>([
        TEE_TIMES_READ,
        TEE_TIMES_CHECKIN,
        MEMBERS_READ,
        MEMBERS_OVERVIEW_READ,
        LEDGER_PAYMENT,
        COURSES_READ,
        PRODUCTS_READ,
        PRODUCTS_STOCK,
        TABS_READ,
        TABS_WRITE,
        TABS_PAYMENT,
        TABS_SETTLE,
      ]),
    ],

    [
      GREENKEEPER,
      new Set<string>([
        TEE_TIMES_READ,
        COURSES_READ,
        NINES_READ,
        MAINTENANCE_READ,
        MAINTENANCE_WRITE,
      ]),
    ],

    [STARTER, new Set<string>([TEE_TIMES_READ, TEE_TIMES_CHECKIN])],
  ]);

/**
 * Expand a set of role names into the union of their permissions. Unknown
 * role names are silently ignored — the security boundary is "user gets
 * only what mapped roles grant," not "block on unrecognized role." Keeps
 * the app forward-compatible if Keycloak introduces a role before the
 * client's matrix catches up.
 *
 * Analogous to the server's `RolePermissions.PermissionsFor`.
 */
export function permissionsFor(roles: string[]): Set<string> {
  const result = new Set<string>();
  for (const role of roles) {
    const perms = ROLE_PERMISSIONS.get(role);
    if (perms) {
      for (const p of perms) result.add(p);
    }
  }
  return result;
}
