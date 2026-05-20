# Dev environment

Brings up **Caddy** (reverse proxy + TLS, mirrors production) and **Keycloak** (OIDC identity provider) as containers. The API and SPA themselves stay outside containers — they run via `just dev-server` and `just dev-client` for hot reload — and Caddy reverse-proxies to them.

Topology mirrors `deploy/pi/`:

```
https://localhost:8443/         → Vite dev server  (host:5173)
https://localhost:8443/api/*    → .NET API         (host:5210)
https://localhost:8443/auth/*   → Keycloak         (container, host:8081)
```

Resource cost: Caddy ~30MB RAM, Keycloak ~512MB. Keep that in mind running alongside the rest of the stack.

## 1. Start the stack

From `deploy/dev/`:

```sh
podman compose up -d
# or: docker compose up -d
```

Caddy and Keycloak both start. Realm at `keycloak/fairway-hq-realm.json` is imported on Keycloak's first boot.

## 2. Install the dev CA cert (one-time)

Caddy generates an internal CA and signs a localhost cert on first run. Your browser and `.NET`'s `HttpClient` will reject the cert until you trust the CA:

```sh
# Copy the root CA out of the Caddy container
podman cp fairway-caddy:/data/caddy/pki/authorities/local/root.crt /tmp/caddy-dev-root.crt

# Fedora / RHEL
sudo trust anchor /tmp/caddy-dev-root.crt

# Ubuntu / Debian
sudo cp /tmp/caddy-dev-root.crt /usr/local/share/ca-certificates/caddy-dev-root.crt
sudo update-ca-certificates

# macOS
sudo security add-trusted-cert -d -r trustRoot \
  -k /Library/Keychains/System.keychain /tmp/caddy-dev-root.crt
```

Restart your browser. After the trust is in place, `dotnet run` and the SPA both validate the cert without warnings.

If you'd rather avoid the CA dance, you can hit `http://localhost:5173/` (Vite directly) for the SPA and `http://localhost:5210/` for the API — but you lose the single-origin routing and have to do CORS, which isn't a great way to spend an afternoon.

## 3. Verify

```sh
curl https://localhost:8443/auth/realms/fairway-hq/.well-known/openid-configuration
```

Should return the OIDC discovery doc with `issuer: https://localhost:8443/auth/realms/fairway-hq`. If you get a TLS error, the CA install didn't take.

Keycloak admin console is reachable two ways:

- Through the proxy: <https://localhost:8443/auth/admin>
- Direct (bypasses Caddy): <http://localhost:8081/auth/admin>

Login `admin` / `admin` (master realm).

## 4. Demo users (dev only — do not ship)

Password equals username for all of these. Each is mapped to a single realm role.

| Username | Password | Role |
| --- | --- | --- |
| `owner` | `owner` | `owner` |
| `manager` | `manager` | `manager` |
| `pro` | `pro` | `pro` |
| `assistant-pro` | `assistant-pro` | `assistant-pro` |
| `pro-shop` | `pro-shop` | `pro-shop` |
| `greenkeeper` | `greenkeeper` | `greenkeeper` |
| `starter` | `starter` | `starter` |

## 5. API configuration

```sh
export Authentication__Keycloak__Authority=https://localhost:8443/auth/realms/fairway-hq
export Authentication__Keycloak__Audience=fairway-hq-spa
just dev-server
```

(Double-underscore = config section separator in ASP.NET environment variable binding.)

The API validates tokens via the JWKS endpoint at the same URL. That fetch happens over HTTPS through Caddy, so the dev CA must be trusted on the host (step 2).

## 6. SPA configuration

```sh
# client/.env.local
VITE_KEYCLOAK_URL=https://localhost:8443/auth
VITE_KEYCLOAK_REALM=fairway-hq
VITE_KEYCLOAK_CLIENT_ID=fairway-hq-spa
```

Run the dev server:

```sh
just dev-client
```

Open <https://localhost:8443/> in the browser. The SPA redirects to the Keycloak login.

## 7. Tear down / reset

```sh
podman compose down
```

`start-dev` uses an in-memory H2 database, so `down` wipes Keycloak state. The next `up` re-imports the realm from JSON. To edit users/roles permanently, change the realm JSON and restart — admin-console edits do **not** persist across container restarts. Caddy's CA persists in the named volume `caddy_data`, so the trust install holds across restarts.

To start over from scratch (new CA cert, new Keycloak state):

```sh
podman compose down -v
```
