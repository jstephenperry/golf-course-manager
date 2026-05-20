# ADR 0003 — Authentication via Keycloak + fine-grained RBAC

**Status**: Accepted
**Date**: 2026-05-19

## Context

The system currently has no authentication or authorization — every endpoint and page is accessible to anyone who can reach the API. As deployments mature beyond a single demo, three realities force the issue:

- **Staff roles have genuinely different concerns**. A greenkeeper logging in to update maintenance status doesn't need to (and shouldn't) see member balances, edit pricing, or rearrange the weekly schedule.
- **The API is the only thing standing between a script kiddie on the WiFi and `POST /api/clear`**. Anything other than zero auth is better than today, and "internal trusted network" is not a real boundary at a small business.
- **Audit and accountability** require identity. Right now any change is anonymous; debugging "who deleted this tee time" is impossible.

This ADR commits to a real auth story.

## Goals

- A single identity provider (Keycloak) owns user accounts, password hygiene, MFA, session management.
- The app verifies tokens via standard OIDC / JWT mechanics — no custom auth code in the API.
- Authorization is **fine-grained and permission-based**, not coarse-grained role checks scattered through the code.
- Every API endpoint and every UI route requires authentication. Default-deny — no opt-out for "internal" endpoints.
- A user with the wrong role gets a clean 403 (API) or a "you don't have access" UI affordance, not a confusing crash.
- The existing test suite continues to pass under the new auth model.
- Staff entities (HR records) remain decoupled from User identities (Keycloak). A future ADR can link them via an external-id mapping.

## Non-goals (this iteration)

- Member-facing logins (members booking their own tee times via a self-service portal). v1 is staff-only.
- Per-tenant isolation. The app is single-tenant per Pi — one Keycloak realm per installation.
- SCIM / directory provisioning. Manual user creation in Keycloak is fine at this scale.
- Step-up auth (MFA-required-for-this-action). MFA happens at login if Keycloak is configured for it; the app doesn't escalate.
- Delegation / impersonation.
- Token revocation beyond Keycloak's native session-management.

## Architecture

```
                 ┌─────────────────┐
                 │   Browser SPA   │
                 │  (React/Vite)   │
                 └────────┬────────┘
                          │
            ┌─────────────┼─────────────┐
            │  PKCE login │ Bearer JWT  │
            │             │             │
            ▼             │             ▼
   ┌────────────────┐     │     ┌────────────────┐
   │   Keycloak     │     │     │  ASP.NET API   │
   │   (OIDC IdP)   ◄─────┘     │                │
   │                │           │  - Authn:      │
   │  - users       │           │    JwtBearer   │
   │  - roles       │           │    (JWKS)      │
   │  - sessions    │           │  - Authz:      │
   └────────┬───────┘           │    policies    │
            │                   │    per permsn  │
            │ JWKS              │                │
            └──────────────────►│                │
                                └────────────────┘
```

**Identity flow**:
1. User opens the SPA, which checks for a stored access token.
2. If absent or expired, SPA initiates an OIDC Authorization Code + PKCE flow against Keycloak.
3. User authenticates with Keycloak (username/password, optionally MFA).
4. Keycloak redirects back to the SPA with an auth code; SPA exchanges it for access + refresh tokens.
5. SPA stores tokens in memory (NOT localStorage — XSS surface), uses access token as `Authorization: Bearer` on every API call.
6. SPA refreshes the access token via Keycloak when it nears expiry.

**API verification**:
- ASP.NET `JwtBearerAuthentication` middleware validates each Bearer token against Keycloak's JWKS endpoint (`/realms/<realm>/protocol/openid-connect/certs`).
- Issuer + audience are validated. Signature is verified against the cached JWKS.
- A custom `IClaimsTransformation` expands the user's `realm_access.roles` claim into permission claims (`perm:tee-times:read`, etc.).
- Policy-based authorization: each endpoint declares a permission requirement; the policy passes when the user has that permission claim.

## Role and permission model

### Roles (Keycloak realm roles)

| Role | Intent |
| --- | --- |
| `owner` | Course owner. All permissions including destructive ops. |
| `manager` | Head pro / GM. All operations except destructive system ops (clear / restore snapshot) and admin (remote-admin endpoints). |
| `pro` | PGA pro. Tee times, lessons, member updates (no suspend), tabs, tournaments. |
| `assistant-pro` | Junior pro / starter. Tee times read+checkin+complete, tabs, member read. |
| `pro-shop` | Pro shop / front counter. Tabs, products, stock, tee-time check-in. |
| `greenkeeper` | Grounds crew. Maintenance tasks, tee-time read-only (to know what's busy), course read-only. |
| `starter` | Tee sheet check-in only. Smallest role — single-purpose login for a kiosk near the first tee. |

### Permission catalog

Permissions are namespaced strings. Each represents a specific action on a resource. Permissions are not exposed in Keycloak — only roles are. The app maps roles to permissions in code, which lets us evolve the matrix without re-issuing tokens.

```
# Tee times
tee-times:read
tee-times:write
tee-times:checkin
tee-times:cancel

# Members
members:read
members:write
members:suspend
members:applications:read
members:applications:write
members:overview:read

# Member ledger / accounting
ledger:read
ledger:charge
ledger:payment
ledger:void
dunning:run

# Courses / nines
courses:read
courses:write
nines:read
nines:write

# Staff / scheduling
staff:read
staff:write
shifts:read
shifts:write
templates:read
templates:write

# Pro shop
products:read
products:write
products:stock

# Tournaments
tournaments:read
tournaments:write

# Maintenance
maintenance:read
maintenance:write

# Tabs (POS)
tabs:read
tabs:write
tabs:payment
tabs:settle
tabs:void

# System
import:run
snapshot:export
snapshot:restore
system:clear
system:health:read

# Remote admin (future ADR 0002 work)
admin:read
admin:write
admin:host
```

### Role → permission matrix

| Permission | owner | manager | pro | assistant-pro | pro-shop | greenkeeper | starter |
| --- | :---: | :---: | :---: | :---: | :---: | :---: | :---: |
| `tee-times:read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `tee-times:write` | ✓ | ✓ | ✓ | ✓ |   |   |   |
| `tee-times:checkin` | ✓ | ✓ | ✓ | ✓ | ✓ |   | ✓ |
| `tee-times:cancel` | ✓ | ✓ | ✓ |   |   |   |   |
| `members:read` | ✓ | ✓ | ✓ | ✓ | ✓ |   |   |
| `members:write` | ✓ | ✓ | ✓ |   |   |   |   |
| `members:suspend` | ✓ | ✓ |   |   |   |   |   |
| `members:applications:read` | ✓ | ✓ | ✓ |   |   |   |   |
| `members:applications:write` | ✓ | ✓ |   |   |   |   |   |
| `members:overview:read` | ✓ | ✓ | ✓ | ✓ | ✓ |   |   |
| `ledger:read` | ✓ | ✓ | ✓ |   |   |   |   |
| `ledger:charge` | ✓ | ✓ |   |   |   |   |   |
| `ledger:payment` | ✓ | ✓ |   |   | ✓ |   |   |
| `ledger:void` | ✓ | ✓ |   |   |   |   |   |
| `dunning:run` | ✓ | ✓ |   |   |   |   |   |
| `courses:read` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |   |
| `courses:write` | ✓ | ✓ |   |   |   |   |   |
| `nines:read` | ✓ | ✓ | ✓ | ✓ |   | ✓ |   |
| `nines:write` | ✓ | ✓ |   |   |   |   |   |
| `staff:read` | ✓ | ✓ |   |   |   |   |   |
| `staff:write` | ✓ | ✓ |   |   |   |   |   |
| `shifts:read` | ✓ | ✓ |   |   |   |   |   |
| `shifts:write` | ✓ | ✓ |   |   |   |   |   |
| `templates:read` | ✓ | ✓ |   |   |   |   |   |
| `templates:write` | ✓ | ✓ |   |   |   |   |   |
| `products:read` | ✓ | ✓ | ✓ | ✓ | ✓ |   |   |
| `products:write` | ✓ | ✓ |   |   |   |   |   |
| `products:stock` | ✓ | ✓ | ✓ |   | ✓ |   |   |
| `tournaments:read` | ✓ | ✓ | ✓ | ✓ |   |   |   |
| `tournaments:write` | ✓ | ✓ | ✓ |   |   |   |   |
| `maintenance:read` | ✓ | ✓ | ✓ |   |   | ✓ |   |
| `maintenance:write` | ✓ | ✓ |   |   |   | ✓ |   |
| `tabs:read` | ✓ | ✓ | ✓ | ✓ | ✓ |   |   |
| `tabs:write` | ✓ | ✓ | ✓ | ✓ | ✓ |   |   |
| `tabs:payment` | ✓ | ✓ | ✓ | ✓ | ✓ |   |   |
| `tabs:settle` | ✓ | ✓ | ✓ | ✓ | ✓ |   |   |
| `tabs:void` | ✓ | ✓ |   |   |   |   |   |
| `import:run` | ✓ |   |   |   |   |   |   |
| `snapshot:export` | ✓ | ✓ |   |   |   |   |   |
| `snapshot:restore` | ✓ |   |   |   |   |   |   |
| `system:clear` | ✓ |   |   |   |   |   |   |
| `system:health:read` | ✓ | ✓ |   |   |   |   |   |

**Highlights of the model**:
- The greenkeeper's example from the goal statement: no `products:*`, no `shifts:*`, no `templates:*` — confirmed.
- The pro-shop role can take payments but not void them. Voiding is a write-back-on-the-day operation that should require a manager.
- Destructive system operations (`system:clear`, `snapshot:restore`, `import:run`) are owner-only.
- `members:applications:*` are pro-and-up. Front-counter staff don't review applications.

## Endpoint → permission mapping (will land in code as comments)

The mapping below is the source of truth slice 3 implements. Listed by endpoint file.

### CrudEndpoints
```
GET    /api/members              members:read
POST   /api/members              members:write
PUT    /api/members/{id}         members:write
DELETE /api/members/{id}         members:write

GET    /api/courses              courses:read
POST   /api/courses              courses:write
PUT    /api/courses/{id}         courses:write
DELETE /api/courses/{id}         courses:write

GET    /api/tee-times            tee-times:read
POST   /api/tee-times            tee-times:write
PUT    /api/tee-times/{id}       tee-times:write       (status==Checked In → tee-times:checkin; status==Cancelled → tee-times:cancel; check at code level)
DELETE /api/tee-times/{id}       tee-times:cancel

GET    /api/staff                staff:read
POST   /api/staff                staff:write
PUT    /api/staff/{id}           staff:write
DELETE /api/staff/{id}           staff:write

GET    /api/shifts               shifts:read
POST   /api/shifts               shifts:write
PUT    /api/shifts/{id}          shifts:write
DELETE /api/shifts/{id}          shifts:write

GET    /api/weekly-templates     templates:read
POST   /api/weekly-templates     templates:write
PUT    /api/weekly-templates/{id} templates:write
DELETE /api/weekly-templates/{id} templates:write

GET    /api/products             products:read
POST   /api/products             products:write
PUT    /api/products/{id}        products:write
POST   /api/products/{id}/adjust-stock  products:stock
DELETE /api/products/{id}        products:write

GET    /api/tournaments          tournaments:read
POST   /api/tournaments          tournaments:write
PUT    /api/tournaments/{id}     tournaments:write
DELETE /api/tournaments/{id}     tournaments:write

GET    /api/maintenance          maintenance:read
POST   /api/maintenance          maintenance:write
PUT    /api/maintenance/{id}     maintenance:write
DELETE /api/maintenance/{id}     maintenance:write
```

### LedgerEndpoints
```
GET    /api/members/{id}/ledger                ledger:read
POST   /api/members/{id}/charges               ledger:charge
POST   /api/members/{id}/payments              ledger:payment
POST   /api/members/ledger/{entryId}/void      ledger:void
```

### MembershipEndpoints
```
GET    /api/applications                       members:applications:read
POST   /api/applications                       members:applications:write
PUT    /api/applications/{id}                  members:applications:write
POST   /api/applications/{id}/approve          members:applications:write
POST   /api/applications/{id}/reject           members:applications:write
POST   /api/applications/{id}/activate         members:applications:write
POST   /api/applications/{id}/withdraw         members:applications:write
DELETE /api/applications/{id}                  members:applications:write
POST   /api/members/{id}/suspend               members:suspend
POST   /api/members/{id}/reinstate             members:suspend
POST   /api/dunning/run                        dunning:run
```

### TabsEndpoints
```
GET    /api/tabs                tabs:read
GET    /api/tabs/{id}           tabs:read
POST   /api/tabs                tabs:write
PUT    /api/tabs/{id}           tabs:write
POST   /api/tabs/{id}/void      tabs:void
POST   /api/tabs/{id}/settle    tabs:settle
POST   /api/tabs/{id}/reopen    tabs:write
... (line items + payments same: tabs:write for items, tabs:payment for payments)
```

### NinesEndpoints
```
GET    /api/nines               nines:read
GET    /api/nines/{id}          nines:read
POST   /api/nines               nines:write
PUT    /api/nines/{id}          nines:write
DELETE /api/nines/{id}          nines:write
```

### MemberOverviewEndpoints
```
GET    /api/members/{id}/overview              members:overview:read
```

### ImportEndpoints
```
POST   /api/import/*            import:run     (single permission, all entities)
```

### OpsEndpoints
```
GET    /api/health              anonymous (only exception)
GET    /api/snapshot            snapshot:export
POST   /api/snapshot/restore    snapshot:restore
POST   /api/reset               system:clear   (reset clears + reseed; treat as destructive)
POST   /api/clear               system:clear
```

## Token validation details

- **Issuer**: `https://<keycloak-host>/realms/<realm>` — comes from `Authentication:Keycloak:Authority` config.
- **Audience**: the client ID configured for the SPA — typically `fairway-hq-spa`. Audience validation enabled.
- **Signing keys**: fetched from JWKS endpoint, cached, refreshed periodically. No shared secrets in the app.
- **Token lifetime**: access tokens 5–15 minutes (Keycloak default); refresh tokens longer-lived. SPA handles silent refresh.
- **Clock skew tolerance**: 30s (ASP.NET default).
- **Required claims**: `sub`, `iss`, `exp`, `realm_access.roles`. The `preferred_username` and `email` claims are surfaced in the UI for display but are not security-relevant.

## Test strategy

The existing 58-test suite uses `WebApplicationFactory<Program>` with in-memory SQLite. It expects unauthenticated requests to succeed everywhere. Adding auth would fail every test.

**The test-only auth scheme**:
- `ApiFactory` adds a `TestAuthHandler` registered under scheme name `Test`.
- In `Testing` environment (already set by ApiFactory: `builder.UseEnvironment("Testing")`), the default authentication scheme becomes `Test`.
- The handler reads two headers from each request:
  - `X-Test-Roles: owner,manager` — list of realm roles
  - `X-Test-User: stephen@example.com` — synthetic identity
- Default (no header) → an `owner` identity is synthesized. Existing tests that don't care about RBAC keep working unchanged.
- New permission-specific tests will set `X-Test-Roles` explicitly to assert 403 paths.

This is the same pattern used in nearly every ASP.NET test setup. It keeps the real auth pipeline (JwtBearer + claims transformer + policies) covered in dev and production but bypasses the cryptography in tests.

## Frontend approach

- **Library**: `oidc-client-ts` (the modern fork of oidc-client). Well-maintained, lightweight, framework-agnostic.
- **Storage**: access + refresh tokens in memory only. On page refresh, silent renew via Keycloak iframe / refresh-token flow. Tradeoff: every cold-start does an OIDC round-trip. Worth it to avoid XSS-stealable tokens.
- **AuthContext**: exposes `user`, `roles`, `permissions`, `hasPermission(perm)`, `login()`, `logout()`.
- **`<ProtectedRoute>`** wrapper around every route except `/login`. Pulls intended destination from `location` and redirects after login.
- **`<RequirePermission>`** component for hiding sub-tree based on permission.
- **Permissions on the client**: the SPA computes permissions from roles using the same role→permission matrix the server uses. The matrix is duplicated between client and server — this is deliberate. The server is the security boundary; the client is UX optimization. Drift is allowed; security is preserved as long as the server's matrix is canonical.
- **Fetch wrapper**: `request()` in `api/client.ts` adds `Authorization: Bearer <token>` from the auth context. On 401 it triggers a silent renew and retries once. On second 401, redirects to login.

## Keycloak deployment

For the dev environment:
- New file `deploy/dev/compose.keycloak.yaml` — separate compose file so dev developers can opt into Keycloak when working on auth. Daily dev (no auth needed) ignores it.
- Image: `quay.io/keycloak/keycloak:26`
- Memory: ~512MB minimum. Document this in setup-dev.md.
- Mode: `start-dev` (in-memory dev database). Production deployments use a separate ADR.
- Realm: `fairway-hq`. Realm export JSON committed at `deploy/dev/keycloak/fairway-hq-realm.json`. Imported on container start.
- Client: `fairway-hq-spa` (public, PKCE, no client secret).
- Demo users baked into the realm export, one per role, with passwords like `owner:owner` / `manager:manager` so dev login is one click.

For the on-prem Pi: Keycloak does NOT run on the Pi. **The on-prem deployment story for auth is its own ADR (0004)** — likely options include:
- Cloud-hosted Keycloak (one instance, one realm per course).
- Per-course local Keycloak on a beefier Pi 5 (8GB) — possible but tight.
- Switch to a lighter IdP (Ory Hydra + Kratos, Authentik) for on-prem.

For now, the API simply talks to whatever IdP its `Authentication:Keycloak:Authority` points at. The choice of where that IdP lives is deferred.

## Open questions (small)

1. **Permission claims size**: 7 roles × ~30 permissions could mean ~200 claims in a token. Most JWT issuers chunk under 4KB. Worst case: `owner` user has ~40 permissions claims. Should still fit comfortably. Confirm at slice 2.
2. **Endpoint-specific override for tee-time PUT**: setting status to `Cancelled` should require `tee-times:cancel`, not generic `tee-times:write`. Decision: enforce at the handler level via a second `IAuthorizationService` check; declare the endpoint with the looser `tee-times:write` policy.
3. **Health endpoint**: `/api/health` stays anonymous. Anything else? Probably not — even `/api/info` (if we add one) wants auth so we don't leak version/uptime.

## Alternatives considered

| Alternative | Why we didn't pick it |
| --- | --- |
| **Roll our own auth** | One-day cost, lifetime debt. Password hashing, session management, MFA, reset flows — all solved problems. Don't reinvent. |
| **Casbin / OPA for policy** | Powerful, more complex. ASP.NET's built-in `IAuthorizationPolicyProvider` + claim-based policies is plenty for this matrix. |
| **Roles-only (no permissions)** | Coarse-grained role checks (`[Authorize(Roles="manager,owner")]`) scattered everywhere. Permissions add one indirection but make matrix changes safer. |
| **Permissions issued by Keycloak as scopes** | Keycloak supports this via Authorization Services. Powerful, also complex to administer. Hybrid (Keycloak issues roles, app derives permissions) is simpler and easier to evolve. |
| **JWT in localStorage** | XSS-grabbable. In-memory + silent renew is the modern best practice for SPA. Refresh requires one OIDC roundtrip on cold start; acceptable. |
| **BFF (Backend-for-Frontend) pattern with HttpOnly cookies** | Slightly better security for hostile environments, but requires API session state. Not justified at this trust level. Revisit if we ever expose this on the open internet. |

## Build order

(Matches the task list — tracked there too.)

1. ADR (this doc)
2. Permission catalog + role mapping in code
3. Backend JWT auth foundation
4. Apply `[Authorize]` per the endpoint mapping above
5. Fix test suite (TestAuthHandler + default-owner)
6. Frontend OIDC client + auth store
7. Login page, protected routes, role-aware UI
8. Keycloak dev compose + realm export
9. End-to-end verification, all green

## References

- OIDC + PKCE: https://www.rfc-editor.org/rfc/rfc7636
- Keycloak admin guide: https://www.keycloak.org/docs/latest/server_admin/
- ASP.NET policy-based authorization: https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies
- ADR 0002 (remote admin) — admin endpoints will use the `admin:*` permissions from this ADR.
