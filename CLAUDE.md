# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Fairway HQ is a single-tenant operations app for one golf course (tee times, members, courses, staff, pro shop, player tabs/POS, tournaments, maintenance). One Keycloak realm + one SQLite file per deployment. Backend: ASP.NET Core **.NET 10** minimal APIs + EF Core/SQLite. Frontend: React 18 + TypeScript + Vite. In production the API serves the built SPA from `wwwroot/` and the JSON API under `/api/*` as **one deployable**.

> Note: `README.md` says ".NET 8" in places — the project has since retargeted to **.NET 10** (`net10.0`, SDK 10). Trust the csproj/Containerfile over the README.

## Commands

Recipes live in the `Justfile` (`just --list`). Common ones:

```bash
# Build / typecheck
just build                       # client + server
cd server && dotnet build        # server only
cd client && npm run typecheck   # tsc -b --noEmit

# Tests
just test                        # server + client
cd server && dotnet test         # xUnit integration suite (91 tests)
cd client && npm test            # vitest one-shot

# Run a single server test (xUnit + dotnet)
cd server && dotnet test --filter "FullyQualifiedName~LedgerServiceTests"
cd server && dotnet test --filter "DisplayName~void"

# Run a single client test file / case (vitest)
cd client && npx vitest run src/test/utils.test.ts
cd client && npx vitest run -t "formats currency"

# E2E (Playwright — boots API serving the built SPA, then drives Chromium)
cd client && npm run e2e:install   # once
cd client && npm run e2e

# EF Core migrations
just migration-add <Name>        # add migration (Data/Migrations)
just migration-update            # apply to local dev db
# The dotnet-ef tool is pinned in .config/dotnet-tools.json — run `dotnet tool restore` first.

just ci                          # install → typecheck → test → build (mirrors GitHub Actions)
```

Dev loops: `just dev-server` (API on :5210) + `just dev-client` (Vite on :5173, proxies `/api`). `just dev-integrated` builds the SPA into `wwwroot` and runs the API single-process (matches prod).

## The ledger is the spine — read this before touching money

Member balances and the financial audit trail are the most invariant-laden part of the system. **All balance mutations MUST go through `Services/MemberAccountService.cs`** (`PostCharge`/`PostPayment`/`VoidEntry`). The `MemberLedgerEntry` table is append-only and is the source of truth; `Member.Balance` is a *cache* reconciled from it.

- Do **not** set `Member.Balance`, `Status`, `Active`, `OldestUnpaidChargeAt`, or `SuspendedAt` from a request body. The member update path uses `MemberUpdateDto` (profile fields only); status changes go through the suspend/reinstate endpoints, balance through the ledger endpoints. Bypassing this was a real privilege-escalation bug — keep it closed.
- Money is C# `decimal` but persisted to SQLite as **TEXT** via an invariant-culture value converter (`Data/AppDbContext.cs`). Never do SQL-side `ORDER BY`/range comparisons on money columns (they compare lexically). Aggregate in memory.
- Round money through `Services/Money.cs` (`Money.Round`, `Money.IsOwed`) — e.g. tab settle. Don't hand-roll epsilons.
- `Member`, `Product`, and `PlayerTab` carry an `int Version` optimistic-concurrency token; balance/stock mutations are wrapped in `Services/ConcurrencyRetry.cs`. Preserve this when adding write paths that touch those entities.
- Tab lifecycle: `settle` 400s if balance > 0; `void` reverses stock + member charges; `reopen` is blocked on Voided tabs. POS payments with `method="Member Charge"` against a Suspended member return 400 `member_suspended`.
- Timestamps are ISO-8601 round-trip (`"o"`) strings, ordered **ordinally** everywhere (SQLite BINARY collation server-side, `StringComparer.Ordinal` in memory). Keep new comparisons ordinal or they'll disagree.

`Services/DunningService.cs` is a background `BackgroundService` (also triggerable via `POST /api/dunning/run`): it suspends Active members whose oldest unpaid charge exceeds `Dunning:PastDueDays`, and reinstatement happens automatically when a payment returns the balance to zero. Tunables under the `Dunning` config section.

## Authentication & RBAC (see `docs/decisions/0003-auth-rbac.md` for the full matrix)

Permission-based authorization, not role checks. The flow and the *exact* endpoint→permission and role→permission tables are documented in ADR 0003 — consult it rather than re-deriving.

- **Server is the security boundary.** Every endpoint declares `RequireAuthorization(Permissions.X)`; a default-deny `FallbackPolicy` locks down anything that forgets to. Only `/api/health` and the SPA fallback are anonymous.
- Permission catalog + role→permission map live in `server/.../Authorization/` (`Permissions.cs`, `RolePermissions.cs`, `Roles.cs`). A claims transformer expands the token's `realm_access.roles` into permission claims; `PermissionPolicyProvider` builds policies on demand.
- **The client deliberately duplicates the role→permission matrix** (`client/src/auth/permissions.ts`, `rolePermissions.ts`, `roles.ts`). It's UX only (nav filtering in `Layout.tsx`, route gating via `<ProtectedRoute requirePermission=…>`, sub-tree gating via `<RequirePermission>`). If you change permissions, update **both** sides — but security is preserved as long as the server matrix is canonical.
- Audit fields like application `ReviewedBy` are stamped server-side from the validated token (`preferred_username`), never trusted from the body.

### Environment-dependent auth (this trips people up)

`Authorization/AuthSetup.cs` branches on `IHostEnvironment`:

- **Testing** → only `TestAuthHandler`. Tests authenticate via headers: `X-Test-Roles: owner,manager` and `X-Test-User: …`. **No header ⇒ a synthesized `owner`**, so RBAC-agnostic tests just work; 403-path tests set `X-Test-Roles` explicitly.
- **Development / Production** → real Keycloak JWT bearer. Issuer + audience (or `azp`) are validated; signing keys come from the backchannel `MetadataAddress`. `RequireHttpsMetadata` defaults to `env.IsProduction()`, which is why dev can fetch JWKS over plain HTTP on the compose network.
- **Production fails closed**: `AuthSetup.ValidateKeycloakConfig` *throws at startup* if `Authentication:Keycloak:Authority` is empty. There is no unauthenticated prod mode.

## Backend layout

`Program.cs` is the composition root: configures the DbContext (SQLite, `SplitQuery`), JSON (camelCase), options (`Dunning`, `Membership`), `DunningService`, auth, DataProtection (keys persisted to `DataProtection:KeyRingPath` when set — the container points it at `/app/data/keys`), then maps endpoint groups and finally runs `db.Database.Migrate()` + `Seed.EnsureSeeded(db)` on boot.

- `Endpoints/*.cs` — feature-grouped minimal-API `Map…` extension methods (Crud, Ledger, Membership, Tabs, Nines, MemberOverview, Import, Ops). Business logic currently lives partly in these handlers; the ledger logic is the exception (centralized in the service).
- `Models/` — `Entities.cs` (EF), `Dtos.cs` (wire shapes), `Mappers.cs` (`ToDto`/`Apply`). Some list fields (tee-time players, registrants) are stored as JSON-in-TEXT blobs and parsed in `Mappers.cs`.
- `Services/Validation.cs` — lightweight, dependency-free input validation (rejects negative money, unknown status/tier strings; `InitiationFee` bounded by `MembershipOptions`).
- Kestrel max request body is capped (32 MiB) and import/restore enforce row-count limits.

## Frontend layout

- `data/store.tsx` — one large React context holding all collections, loading/error flags, and the action methods, with a 30s background poll. `api/client.ts` is the fetch wrapper: attaches `Authorization: Bearer` from the auth context and, on 401, does a single silent token renew + retry before failing.
- `auth/AuthContext.tsx` — `oidc-client-ts` Authorization-Code + PKCE; **access tokens kept in memory, not localStorage** (only PKCE/state in storage). Exposes `user`, `roles`, `permissions`, `hasPermission`.
- `pages/` are route components (lazy-loaded, code-split in `App.tsx`); `components/` holds shared UI (modals, `ErrorBoundary`, `Toaster`).

## Deployment / containers

- `Containerfile` — multi-stage (SPA build → API publish → `aspnet:…-chiseled-extra` runtime). Runs **non-root (UID 1654)**, base images digest-pinned, DB on the `/app/data` volume (`ConnectionStrings__Default`). The chiseled runtime has **no shell/curl** — the healthcheck is a bundled .NET probe (`scripts/healthcheck/`). `VITE_KEYCLOAK_*` are **baked at build time** as build args (one image targets one IdP).
- Compose files: root `compose.yaml` (local single-container smoke test), `deploy/dev/` (full prod-shaped stack: Caddy + Keycloak + app — see `deploy/dev/setup-dev.md`), `deploy/pi/` (production; auth env vars required via `${VAR:?}`). **Podman builds OCI-format images, which silently drop the Containerfile `HEALTHCHECK`** — so healthchecks are also declared at the **compose level** (where they work regardless of format). Keep both in sync if you change the probe.
- CI: `.github/workflows/ci.yml` (server/client/e2e jobs), `image.yml` (multi-arch publish to ghcr.io). Actions are SHA-pinned.

## Testing notes

`FairwayHq.Api.Tests/ApiFactory.cs` boots `WebApplicationFactory<Program>` in the `Testing` environment over a **single shared in-memory SQLite connection** — so the suite is serial and cannot exercise true write concurrency (concurrency-token behavior is tested deterministically instead). `Helpers/TestSeed.cs` provisions fixtures; tests provision their own data (no global synthetic seed). Client tests use Vitest + React Testing Library; E2E uses Playwright via `scripts/run-e2e-server.sh`.
