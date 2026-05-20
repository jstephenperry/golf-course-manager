// Mirrors server/FairwayHq.Api/Authorization/*.cs. Keep in sync.
//
// Realm-role names exactly as they appear in Keycloak. The app receives
// whatever roles the token carries and looks them up in
// `rolePermissions.ts`.

export const OWNER = "owner";
export const MANAGER = "manager";
export const PRO = "pro";
export const ASSISTANT_PRO = "assistant-pro";
export const PRO_SHOP = "pro-shop";
export const GREENKEEPER = "greenkeeper";
export const STARTER = "starter";

export const ALL_ROLES: ReadonlySet<string> = new Set<string>([
  OWNER,
  MANAGER,
  PRO,
  ASSISTANT_PRO,
  PRO_SHOP,
  GREENKEEPER,
  STARTER,
]);
