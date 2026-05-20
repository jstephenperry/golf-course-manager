/**
 * Silent-renew callback (loaded by `silent-renew.html` inside the hidden
 * iframe oidc-client-ts opens for prompt=none renewals).
 *
 * Deliberately tiny: it does NOT import React, the store, or the API client.
 * Its only job is to hand the authorization response back to the parent
 * window via `signinSilentCallback()`. Booting the full SPA here is exactly
 * the bug this page exists to avoid — a renewal iframe that mounts the app
 * would fire API calls, 401, and recursively trigger more renewals.
 */
import { UserManager } from "oidc-client-ts";
import { buildUserManagerSettings, readAuthConfig } from "./auth/oidcSettings";

const config = readAuthConfig();
if (config) {
  new UserManager(buildUserManagerSettings(config))
    .signinSilentCallback()
    .catch((err) => {
      // login_required (no active Keycloak session) is expected and benign;
      // the parent window's signinSilent() promise rejects and the app falls
      // through to interactive login. Log for any other failure.
      // eslint-disable-next-line no-console
      console.debug("[auth] silent renew callback ended", err);
    });
}
