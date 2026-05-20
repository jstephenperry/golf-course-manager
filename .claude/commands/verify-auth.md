---
description: Verify the containerized OIDC login/logout flow end-to-end (config layer)
---

Sanity-check the Keycloak + Caddy + SPA auth wiring at `https://localhost:8443`
without a browser. These checks catch the config-layer bugs that broke this flow
before (port-stripping in the discovery doc, silent-renew iframe booting the SPA,
stale image). Run them and report a pass/fail per check.

## Checks

1. **Discovery doc advertises the correct host:port on every endpoint.**
   `curl -sk https://localhost:8443/auth/realms/fairway-hq/.well-known/openid-configuration | tr ',' '\n' | grep -E '"(issuer|authorization_endpoint|token_endpoint|jwks_uri|end_session_endpoint)"'`
   - PASS: every URL is `https://localhost:8443/auth/...`
   - FAIL: any URL drops the port (`https://localhost/auth/...`). Cause: Caddy not
     forwarding `X-Forwarded-Port 8443` to Keycloak (`KC_HOSTNAME_BACKCHANNEL_DYNAMIC`
     derives token/jwks endpoints from the forwarded request). Fix in `deploy/dev/Caddyfile`.

2. **Silent-renew callback is a standalone page, not the SPA.**
   `curl -sk -o /dev/null -w "status=%{http_code} type=%{content_type}\n" https://localhost:8443/silent-renew.html`
   then `curl -sk https://localhost:8443/silent-renew.html | grep -c 'id="root"'`
   - PASS: `200 text/html` AND `id="root"` count is `0`.
   - FAIL (404/401, or count ≥1 = SPA shell): the renewal iframe will boot the whole
     app and storm. Cause: missing `silent-renew.html` (stale image) or `silent_redirect_uri`
     not set in `client/src/auth/oidcSettings.ts`.

3. **Logout endpoint accepts the post-logout redirect.**
   `curl -sk -o /dev/null -w "status=%{http_code} redirect=%{redirect_url}\n" "https://localhost:8443/auth/realms/fairway-hq/protocol/openid-connect/logout?client_id=fairway-hq-spa&post_logout_redirect_uri=https%3A%2F%2Flocalhost%3A8443%2F"`
   - PASS: `302` → `https://localhost:8443/`.

4. **App + API are up.**
   `curl -sk -o /dev/null -w "%{http_code}\n" https://localhost:8443/api/health` (expect 200)

Note: the *client-side* logout race (ProtectedRoute auto-login beating `signoutRedirect`)
and the silent-renew/store-gating behavior can't be seen via curl — they need a browser.
The `isRedirecting` guard in `AuthContext.tsx` and the `useOptionalAuth` gate in
`data/store.tsx` are the relevant code if logout/login misbehaves in the browser.

$ARGUMENTS
