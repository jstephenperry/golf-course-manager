import {
  WebStorageStateStore,
  type UserManagerSettings,
} from "oidc-client-ts";

// ---------------------------------------------------------------------------
// Shared OIDC configuration
//
// Both the React `AuthProvider` and the standalone silent-renew callback
// entry (`src/silent-renew.ts`) build their UserManager from these helpers,
// so the two never drift. The silent-renew page must use the SAME settings
// (authority, client_id, and especially the localStorage `stateStore`) or it
// can't validate the flow state and notify the parent window.
// ---------------------------------------------------------------------------

export interface AuthConfig {
  authority: string;
  clientId: string;
}

/**
 * Read OIDC config from Vite env. Returns null if any required var is
 * missing — callers treat that as "auth disabled" so local dev and tests
 * that don't care about auth can still run.
 */
export function readAuthConfig(): AuthConfig | null {
  const url = import.meta.env.VITE_KEYCLOAK_URL;
  const realm = import.meta.env.VITE_KEYCLOAK_REALM;
  const clientId = import.meta.env.VITE_KEYCLOAK_CLIENT_ID;
  if (!url || !realm || !clientId) return null;
  return {
    authority: `${url}/realms/${realm}`,
    clientId,
  };
}

export function buildUserManagerSettings(
  config: AuthConfig,
): UserManagerSettings {
  const origin =
    typeof window !== "undefined" ? window.location.origin : "http://localhost";
  return {
    authority: config.authority,
    client_id: config.clientId,
    // NOT under /auth/* — the reverse proxy routes that prefix to
    // Keycloak. The interactive callback must hit the SPA upstream, so we
    // use /oidc/callback which Caddy passes through to fairway-hq.
    redirect_uri: `${origin}/oidc/callback`,
    // Silent (prompt=none) renewals land here instead. This is a tiny
    // static page (`silent-renew.html`) that ONLY runs
    // `signinSilentCallback()` — it must not boot the SPA, or the hidden
    // renewal iframe would mount the whole app (store + API calls) and
    // recursively spawn more renewals. Without this, oidc-client-ts falls
    // back to `redirect_uri`, which serves the full SPA.
    silent_redirect_uri: `${origin}/silent-renew.html`,
    post_logout_redirect_uri: `${origin}/`,
    response_type: "code",
    scope: "openid profile email",
    // PKCE verifier / state / nonce — transient OIDC flow state, NOT the
    // access token. localStorage is the right tradeoff: survives the
    // round-trip to Keycloak so the callback can complete.
    stateStore: new WebStorageStateStore({ store: window.localStorage }),
    // Tokens themselves stay in memory (oidc-client-ts default
    // `InMemoryWebStorage`) — never persisted to web storage.
    automaticSilentRenew: true,
    // Fire the expiring event 60s before expiry so a renew can complete
    // before a request 401s.
    accessTokenExpiringNotificationTimeInSeconds: 60,
    monitorSession: false,
  };
}
