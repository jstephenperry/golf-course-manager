# Dev environment

> **DEV ONLY — NOT DEPLOYABLE.** This stack runs Keycloak in `start-dev`
> mode with an in-memory database, demo users whose password equals their
> username, and a local admin account. It exists for local development on
> a trusted machine. Do **not** reuse this compose file, its realm export,
> or these credentials for any production/Pi deployment — use `deploy/pi/`
> (auth required, secrets from `.env`) for that. The admin console here is
> bound to `127.0.0.1` and the admin password has no committed default.

This is a **fully-containerized, production-shaped** stack: Caddy (TLS +
reverse proxy), Keycloak (OIDC identity provider), and the Fairway HQ app
itself (the .NET API serving the bundled SPA) all run as containers on one
compose network. It mirrors `deploy/pi/` exactly — the only difference is
that the IdP runs alongside in this compose rather than externally.

```
https://localhost:8443/         → fairway-hq container (SPA shell)
https://localhost:8443/api/*    → fairway-hq container (.NET API)
https://localhost:8443/auth/*   → fairway-keycloak container (OIDC)
```

Resource cost: Caddy ~30MB RAM, Keycloak ~512MB, app ~150MB.

Hot reload is intentionally **not** part of this mode — it runs the
production topology. Edits to app code require a rebuild
(`podman compose build fairway-hq && podman compose up -d`). For
fast-iteration UI work, see [Native hot-reload](#native-hot-reload-alternative)
at the bottom.

## 1. Start the stack

From `deploy/dev/`:

```sh
# The dev Keycloak admin password has no committed default. Set it once
# (compose auto-loads deploy/dev/.env). Keep it out of git — .env is local.
echo 'KC_ADMIN_PASSWORD=pick-a-dev-password' > .env

# --build is required the first time and after any app code change: the
# fairway-hq image is built from the repo's Containerfile.
podman compose up -d --build
# or: docker compose up -d --build
```

If `KC_ADMIN_PASSWORD` is unset, the stack refuses to start with a clear
error rather than falling back to a baked-in password.

All three services start. Caddy waits for `fairway-hq` to report
**healthy** (a compose-level healthcheck hits `/api/health`) before it
comes up. Keycloak imports the realm from `keycloak/fairway-hq-realm.json`
on first boot.

Watch it come up:

```sh
podman compose ps                 # STATUS column shows (healthy) for fairway-hq
podman compose logs -f            # tail all three services
podman healthcheck run fairway-hq # run the app probe on demand
```

## 2. Install the dev CA cert (one-time)

Caddy generates an internal CA and signs a localhost cert on first run.
Your browser rejects the cert until you trust the CA. Use the helper script
(or follow the manual steps it prints):

```sh
./trust-caddy-ca.sh
```

It copies the root CA out of the `fairway-caddy` container and installs it
into the OS trust store (Fedora/RHEL, Debian/Ubuntu, and macOS supported).
Restart your browser afterward.

> The app container talks to Keycloak over the **internal** compose network
> (`http://fairway-keycloak:8080/auth/...`, plain HTTP — see
> `Authentication__Keycloak__MetadataAddress` in the compose), so the API's
> JWKS fetch does **not** depend on the CA being trusted. The CA trust is for
> your **browser** so the SPA and the Keycloak login page load without
> warnings.

## 3. Verify

```sh
# OIDC discovery through the proxy — issuer must be the PUBLIC URL
curl https://localhost:8443/auth/realms/fairway-hq/.well-known/openid-configuration

# App health (anonymous)
curl https://localhost:8443/api/health        # {"status":"ok",...}

# A protected endpoint with no token must 401 (auth is wired, fails closed)
curl -o /dev/null -w '%{http_code}\n' https://localhost:8443/api/members   # 401
```

The discovery doc's `issuer` should be
`https://localhost:8443/auth/realms/fairway-hq`. A TLS error here means the
CA install (step 2) didn't take.

Keycloak admin console:

- Through the proxy: <https://localhost:8443/auth/admin>
- Direct (loopback only, bypasses Caddy): <http://localhost:8081/auth/admin>

Login `admin` / `$KC_ADMIN_PASSWORD` (master realm) — the value you set in
`deploy/dev/.env`.

## 4. Log in to the app

Open <https://localhost:8443/> — the SPA redirects to the Keycloak login.
Sign in as any demo user below; the role drives what the UI and API allow.

### Demo users (dev only — do not ship)

Password equals username for all of these. Each is mapped to a single realm
role.

| Username | Password | Role |
| --- | --- | --- |
| `owner` | `owner` | `owner` |
| `manager` | `manager` | `manager` |
| `pro` | `pro` | `pro` |
| `assistant-pro` | `assistant-pro` | `assistant-pro` |
| `pro-shop` | `pro-shop` | `pro-shop` |
| `greenkeeper` | `greenkeeper` | `greenkeeper` |
| `starter` | `starter` | `starter` |

The SPA's Keycloak settings (`VITE_KEYCLOAK_URL=https://localhost:8443/auth`,
realm `fairway-hq`, public client `fairway-hq-spa`) are baked into the bundle
at image-build time via the `build.args` in the compose file — there is **no**
`client/.env.local` involved in this containerized mode.

## 5. Tear down / reset

```sh
podman compose down       # stop; keeps Caddy CA + app data volumes
podman compose down -v    # also wipe volumes: new CA, fresh DB, re-import realm
```

`start-dev` uses an in-memory H2 database, so `down` already wipes Keycloak
state and the realm is re-imported on the next `up`. To change users/roles
permanently, edit the realm JSON and restart — admin-console edits do **not**
persist across restarts. The app's SQLite DB and DataProtection key ring live
in the `fairway_data` volume and survive `down` (but not `down -v`).

---

## Native hot-reload (alternative)

For fast UI/server iteration without rebuilding the app image, run the app
and SPA on the host and use the containerized Keycloak only:

```sh
# Just the IdP. (Caddy's upstream points at the app *container*, so in this
# mode you hit Vite directly on :5173 rather than through Caddy.)
podman compose up -d keycloak

# Terminal 1 — API on http://localhost:5210
export Authentication__Keycloak__Authority=https://localhost:8443/auth/realms/fairway-hq
export Authentication__Keycloak__Audience=fairway-hq-spa
just dev-server

# Terminal 2 — SPA on http://localhost:5173 (a redirect URI the realm allows)
just dev-client
```

```sh
# client/.env.local (only needed for native mode)
VITE_KEYCLOAK_URL=https://localhost:8443/auth
VITE_KEYCLOAK_REALM=fairway-hq
VITE_KEYCLOAK_CLIENT_ID=fairway-hq-spa
```

Open <http://localhost:5173/>. The realm's `fairway-hq-spa` client already
allows `http://localhost:5173/*` as a redirect URI and web origin. The API's
JWKS fetch still goes through Caddy over HTTPS, so the dev CA (step 2) must be
trusted on the host in this mode.
