// Mirrors server/FairwayHq.Api/Authorization/*.cs. Keep in sync.
//
// The canonical catalog of permission strings used on the client for
// UX-level role-aware rendering. The server is the security boundary; this
// duplicated catalog is purely so the SPA can hide / disable affordances
// the user isn't authorized to perform. Drift is allowed; the server's
// matrix is canonical.
//
// Permissions are namespaced as `resource:action` and the string values
// MUST match the server constants exactly.

// ----- Tee times -----
export const TEE_TIMES_READ = "tee-times:read";
export const TEE_TIMES_WRITE = "tee-times:write";
export const TEE_TIMES_CHECKIN = "tee-times:checkin";
export const TEE_TIMES_CANCEL = "tee-times:cancel";

// ----- Members -----
export const MEMBERS_READ = "members:read";
export const MEMBERS_WRITE = "members:write";
export const MEMBERS_SUSPEND = "members:suspend";
export const MEMBERS_APPLICATIONS_READ = "members:applications:read";
export const MEMBERS_APPLICATIONS_WRITE = "members:applications:write";
export const MEMBERS_OVERVIEW_READ = "members:overview:read";

// ----- Member ledger / accounting -----
export const LEDGER_READ = "ledger:read";
export const LEDGER_CHARGE = "ledger:charge";
export const LEDGER_PAYMENT = "ledger:payment";
export const LEDGER_VOID = "ledger:void";
export const DUNNING_RUN = "dunning:run";

// ----- Courses / nines -----
export const COURSES_READ = "courses:read";
export const COURSES_WRITE = "courses:write";
export const NINES_READ = "nines:read";
export const NINES_WRITE = "nines:write";

// ----- Staff / scheduling -----
export const STAFF_READ = "staff:read";
export const STAFF_WRITE = "staff:write";
export const SHIFTS_READ = "shifts:read";
export const SHIFTS_WRITE = "shifts:write";
export const TEMPLATES_READ = "templates:read";
export const TEMPLATES_WRITE = "templates:write";

// ----- Pro shop -----
export const PRODUCTS_READ = "products:read";
export const PRODUCTS_WRITE = "products:write";
export const PRODUCTS_STOCK = "products:stock";

// ----- Tournaments -----
export const TOURNAMENTS_READ = "tournaments:read";
export const TOURNAMENTS_WRITE = "tournaments:write";

// ----- Maintenance -----
export const MAINTENANCE_READ = "maintenance:read";
export const MAINTENANCE_WRITE = "maintenance:write";

// ----- Tabs (POS) -----
export const TABS_READ = "tabs:read";
export const TABS_WRITE = "tabs:write";
export const TABS_PAYMENT = "tabs:payment";
export const TABS_SETTLE = "tabs:settle";
export const TABS_VOID = "tabs:void";

// ----- System ops -----
export const IMPORT_RUN = "import:run";
export const SNAPSHOT_EXPORT = "snapshot:export";
export const SNAPSHOT_RESTORE = "snapshot:restore";
export const SYSTEM_CLEAR = "system:clear";
export const SYSTEM_HEALTH_READ = "system:health:read";

// ----- Remote admin (ADR 0002) -----
export const ADMIN_READ = "admin:read";
export const ADMIN_WRITE = "admin:write";
export const ADMIN_HOST = "admin:host";

/**
 * Every permission constant defined above. Useful for tests and the
 * role-mapping sanity check.
 */
export const ALL_PERMISSIONS: ReadonlySet<string> = new Set<string>([
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
  IMPORT_RUN,
  SNAPSHOT_EXPORT,
  SNAPSHOT_RESTORE,
  SYSTEM_CLEAR,
  SYSTEM_HEALTH_READ,
  ADMIN_READ,
  ADMIN_WRITE,
  ADMIN_HOST,
]);
