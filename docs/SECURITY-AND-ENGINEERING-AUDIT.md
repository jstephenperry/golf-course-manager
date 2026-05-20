# Fairway HQ — Security & Engineering Audit

**Date:** 2026-05-20
**Scope:** Full codebase — .NET 10 minimal-API backend (`server/FairwayHq.Api`), React/Vite/TS SPA (`client`), containerization, CI, deploy config.
**Method:** Four parallel specialist reviews (backend security, backend engineering, frontend security+quality, DevSecOps/infra). Read-only.
**Context:** The latest commit (`36b6006`) is flagged *"WIP: containerized stack + Keycloak/RBAC integration (broken)."* Several findings stem directly from that half-finished state.

---

## Executive summary

The codebase is, for its size, well-structured: a clean RBAC permission catalog with default-deny authorization, an append-only ledger funnelled through a single `MemberAccountService`, an OIDC Authorization-Code-+-PKCE SPA that keeps tokens in memory, decent test coverage, and a tidy multi-stage container build with **no committed secrets and a clean git history**.

The serious problems cluster around four root causes:

1. **Authentication is currently unwired and fails *open* under a plausible misconfiguration.** Production ships with OIDC commented out, and issuer validation is gated on a config value that is empty by default. This is the "(broken)" state and the single highest priority.
2. **Mass-assignment through generic CRUD lets callers bypass the entire permission and ledger model.** A plain `PUT /api/members/{id}` can rewrite a member's `Balance` and un-suspend them — bypassing the `ledger:*` and `members:suspend` permissions that exist specifically to protect those operations.
3. **Financial data has correctness hazards independent of security:** money stored as SQLite `TEXT` with no converter, a cached `Member.Balance` with no concurrency control (lost updates), unrounded tax math, and culture-sensitive timestamp comparison in the ledger.
4. **Privileged ops endpoints (`/snapshot/restore`, `/clear`, `/reset`) can rewrite or wipe the whole database** and rebuild the "append-only" ledger from unvalidated client input.

Plus two systemic gaps: client routes are not permission-gated (defense-in-depth only, *if* the server enforces — which makes #1 critical), and audit attribution ("who approved/reviewed") is taken from client-supplied free text instead of the authenticated identity.

### Severity roll-up

| ID | Sev | Title | Primary location |
|----|-----|-------|------------------|
| **A1** | **Critical** | Auth fails open: issuer/audience validation disabled by default; prod ships with OIDC commented out | `Authorization/AuthSetup.cs:61-129`; `deploy/pi/compose.yaml:46-60`; `Containerfile` (no `ASPNETCORE_ENVIRONMENT`) |
| **A2** | **Critical** | `/api/snapshot/restore`, `/clear`, `/reset` wipe DB & rebuild ledger from unvalidated body | `Endpoints/OpsEndpoints.cs:24-203` |
| **A3** | **High** | Mass assignment via `PUT /api/members/{id}` — rewrite `Balance`/`Status`/`Active`, bypassing ledger & suspend permissions | `Endpoints/CrudEndpoints.cs:32-39`, `Models/Mappers.cs:25-40` |
| **A4** | **High** | Audit fields (`ReviewedBy`/reviewer) are client-supplied, not derived from identity | `Endpoints/MembershipEndpoints.cs:71,86`; `client/src/pages/Members.tsx:173,205,214` |
| **A5** | **High** | Money stored as `decimal`→SQLite `TEXT` with no converter → wrong SQL ordering/range | `Data/AppDbContext.cs:95-101` |
| **A6** | **High** | `Member.Balance` cache has no concurrency token → lost updates on concurrent writes | `Models/Entities.cs:22`, `Services/MemberAccountService.cs` |
| **A7** | **High** | Container runs as root; mutable base tags + `:latest` + `autoupdate=registry` | `Containerfile` (no `USER`; lines 10,36,58); `deploy/pi/compose.yaml:28,31,52` |
| **A8** | **High** | Client routes never pass `requirePermission` — page-level authz absent | `client/src/App.tsx:86`; `client/src/auth/ProtectedRoute.tsx` |
| **A9** | **Med** | Unrounded tax/tip math + magic `0.005m` settle epsilon | `Endpoints/TabsEndpoints.cs:99-105` |
| **A10** | **Med** | Ledger pagination uses culture-sensitive `string.Compare` vs ordinal elsewhere | `Endpoints/LedgerEndpoints.cs:34` vs `MemberAccountService.cs:208-209` |
| **A11** | **Med** | No pagination / full-table materialization on list endpoints (esp. `/api/tabs` w/ children, member-overview loads all tee-times) | `CrudEndpoints.cs`, `TabsEndpoints.cs:21-23`, `MemberOverviewEndpoints.cs:27-30` |
| **A12** | **Med** | Stock/tab/balance read-modify-write races (no rowversion); tab `reopen` re-opens voided tabs | `TabsEndpoints.cs:114-122,125-214`, `CrudEndpoints.cs:229-236` |
| **A13** | **Med** | No request-size/collection-count limits on import & restore (DoS) | `ImportEndpoints.cs`, `OpsEndpoints.cs:24` |
| **A14** | **Med** | Initiation fee unbounded & client-controlled; ad-hoc/missing validation across CRUD | `MembershipEndpoints.cs`, `Models/Mappers.cs`, most CRUD endpoints |
| **A15** | **Med** | CI actions not SHA-pinned (workflow has `packages: write`) | `.github/workflows/ci.yml`, `image.yml` |
| **A16** | **Med** | Backup/import automation hits RBAC'd endpoints unauthenticated → silently 401s once auth is on | `deploy/pi/backup.sh:22`, `seed-data/.../import.sh` |
| **A17** | **Med** | `MemberAccountService` results discarded in tab flows (silent error swallow) | `TabsEndpoints.cs:81-85,249-252,277-280` |
| **A18** | **Low** | Dual source of truth: `Member.Active` (bool) vs `Member.Status` (string), hand-synced | `Models/Entities.cs:19-20` |
| **A19** | **Low** | Generic 500 handler logs nothing; inconsistent error shapes (no `ProblemDetails`) | `Program.cs:63-72` |
| **A20** | **Low** | Stringly-typed dates/statuses/enums + JSON-blob list columns w/ swallow-all parse | `Models/Entities.cs`, `Models/Mappers.cs:9-17` |
| **A21** | **Low** | ~90 `<label>` elements with zero `htmlFor` (a11y); native `window.confirm`; client IDs via `Math.random` | `client/src/pages/*`, `client/src/data/store.tsx:860` |
| **A22** | **Low** | Code duplication: `NewId` copy-pasted ×5; ~300 LOC near-identical CRUD; repeated import skeletons | `CrudEndpoints.cs`, `ImportEndpoints.cs`, etc. |
| **A23** | **Low** | Dev Keycloak `admin/admin` & password==username demo users committed (dev-only, not deployable) | `deploy/dev/compose.yaml:40-41`, `deploy/dev/keycloak/fairway-hq-realm.json:52-137` |
| **A24** | **Low** | CI provisions .NET 8 while project targets .NET 10 (build drift); `UseForwardedHeaders` w/o `KnownProxies` | `.github/workflows/ci.yml`; `Program.cs:50-53` |
| **A25** | **Low** | Giant `Store` context → whole-app re-renders on 30s poll; poll/mutation race | `client/src/data/store.tsx` |

> **Verified non-issues (closed loops):** No SQL injection (all EF LINQ, parameterized). `TestAuthHandler` is registered only in the `Testing` environment, unreachable in prod. `fairway.db*` files exist on disk but are **not** git-tracked. No secrets in the SPA bundle or git history; `.env.local` is untracked. Production compose has proper network isolation (app on `expose`, Caddy sole ingress, no privileged containers).

---

## Detailed findings

### A1 — CRITICAL — Authentication fails open / is unwired in production
`Authorization/AuthSetup.cs:61-129` · `deploy/pi/compose.yaml:46-60` · `Containerfile`

The production compose ships with both OIDC keys commented out:
```yaml
# Authentication__Keycloak__Authority: "https://fairway.local:8443/auth/realms/fairway-hq"
# Authentication__Keycloak__Audience: "fairway-hq-spa"
```
With `authority` empty, `TokenValidationParameters` degrade: `ValidateIssuer = !string.IsNullOrEmpty(authority)` → **false** (`:86`); `ValidateAudience = false` is hard-coded (`:100`) and the custom `AudienceValidator` `return true`s when audience is empty (`:103`).

In the *no-config* state the app most likely 401s everything (no JWKS source → signatures can't validate → fails closed, consistent with the Caddyfile note "the app runs without auth"). **But the design is one config slip from full bypass:** because issuer validation is gated solely on `Authority`, an operator who sets only the internal `MetadataAddress` (the exact scenario the comments describe) gets JWKS-validated signatures with **issuer never checked and audience falling through** — any token from that realm, for any client, is accepted against an API exposing member PII, the ledger, POS, and full DB export/restore. The container also never sets `ASPNETCORE_ENVIRONMENT`, defaulting to Production.

**Remediation:**
- In Production, **throw at startup** if `Authority` is empty — fail closed loudly rather than registering a permissive `JwtBearer`.
- Always `ValidateIssuer = true` with an explicit expected issuer; decouple it from `Authority`.
- Set `ValidateAudience = true` (or make empty audience a hard failure, not `return true` at `:103`).
- Ship `deploy/pi/compose.yaml` with the auth vars **uncommented and required**, and set `ASPNETCORE_ENVIRONMENT=Production` explicitly.

### A2 — CRITICAL — Privileged ops can wipe DB and forge the ledger
`Endpoints/OpsEndpoints.cs:24-203`

`POST /api/snapshot/restore` deletes every row in every table (`:28-51`) and rebuilds all entities — including `MemberLedgerEntry` rows verbatim from the request body (`:167-185`) — **bypassing `MemberAccountService`**. Member balances are restored from `MemberDto.Balance` (`Mappers.cs:36`) with no reconciliation against the restored ledger. `/clear` and `/reset` wipe everything. These are correctly `owner`-only, but the "append-only source of truth" guarantee is void here and the blast radius is total — making A1 critical by composition.

**Remediation:** Keep owner-only and additionally: require a step-up/second confirmation; validate that restored ledger sums reconcile to restored balances and reject inconsistent snapshots; reconstruct balances *from* the ledger via `MemberAccountService` rather than trusting `MemberDto.Balance`.

### A3 — HIGH — Mass assignment bypasses the permission & ledger model
`Endpoints/CrudEndpoints.cs:32-39` · `Models/Mappers.cs:25-40`

`Member.Apply(dto)` copies `Balance`, `Status`, `Active`, `OldestUnpaidChargeAt`, `SuspendedAt` straight from the body, and `PUT /api/members/{id}` requires only `members:write` (manager **and** pro). So a `pro` — who lacks `ledger:charge/payment/void` and `members:suspend` — can zero a balance and un-suspend a member with a plain PUT, defeating the entire permission separation and the ledger chokepoint. (A test, `DunningTests.cs:112`, actually relies on this to zero a balance — the smell is baked into the suite.)

**Remediation:** Introduce a dedicated member-update DTO containing only profile fields. Remove `Balance`/`Status`/`Active`/`OldestUnpaidChargeAt`/`SuspendedAt` from `Member.Apply`. Mutate balance only via the ledger endpoints and status only via suspend/reinstate.

### A4 — HIGH — Spoofable audit attribution
`Endpoints/MembershipEndpoints.cs:71,86` · `client/src/pages/Members.tsx:173,205,214`

`ReviewedBy` is set from `body.Reviewer` (server) / a free-text `useState` input defaulting to `"staff"` (client). The authenticated identity (`User`/`preferred_username` server-side, `useAuth().user` client-side) is available but unused, so approval/rejection records — and the initiation charges they trigger — can be attributed to any chosen name.

**Remediation:** Derive reviewer from the validated token server-side and ignore any client-supplied value. Remove the free-text reviewer field from the SPA.

### A5 — HIGH — Money as `TEXT` with no converter
`Data/AppDbContext.cs:95-101`

A blanket loop sets every `decimal`/`decimal?` column to `SetColumnType("TEXT")` with no value converter and no invariant-culture format. Any SQL-side `WHERE Amount > x` or `ORDER BY Amount` becomes a lexicographic string compare (`"100" < "9"`), silently wrong. It works today only because every aggregate is pulled into memory first — a trap for the first server-side aggregation.

**Remediation:** Add an explicit invariant-culture `decimal`→`string` converter with a fixed format (e.g. `F4`), or — preferred — store money as integer minor units (cents).

### A6 — HIGH — `Member.Balance` lost updates
`Models/Entities.cs:22` · `Services/MemberAccountService.cs`

Every balance change is read-modify-write on the cached `Member.Balance` with no rowversion/concurrency token. Two concurrent writers (two POS terminals, or a tab member-charge plus a manual payment) both read the old balance and the second overwrites the first; the ledger entry persists but the cached balance drifts from ground truth — defeating the invariant the design exists to protect. The per-request transaction does not prevent this under SQLite's default isolation.

**Remediation:** Add a rowversion token to `Member` (and `Product`, `PlayerTab`) and handle `DbUpdateConcurrencyException` with retry; or recompute balance from the ledger inside the transaction instead of incrementing the cache.

### A7 — HIGH — Container hardening: root + mutable tags + auto-update
`Containerfile` (no `USER`; `:10,36,58`) · `deploy/pi/compose.yaml:28,31,52`

The runtime stage uses `aspnet:10.0-noble-chiseled-extra` (root by default) with no `USER`, so the process and the `/app/data` SQLite volume are root-owned. Base images use floating tags (`node:20-alpine`, `sdk:10.0`, the aspnet tag) and the app runs `:latest` with `io.containers.autoupdate=registry` + a daily timer — a freshly-published `latest` auto-deploys to every course with no pin or rollback. No `HEALTHCHECK`.

**Remediation:** Add `USER $APP_UID` (1654) and make `/app/data` writable by it; pin base images by digest; pin the app to an immutable version tag (or gate auto-update on a tested channel); add a `HEALTHCHECK` hitting `/api/health`.

### A8 — HIGH (conditional) — Client route authz never wired
`client/src/App.tsx:86` · `client/src/auth/ProtectedRoute.tsx`

`ProtectedRoute` supports a `requirePermission` prop and renders `<Forbidden>` when lacking it, but `App.tsx` never passes it on any route. Only inline action buttons are gated. So any logged-in user can open `/import`, `/staff`, `/members/:id` (ledger/PII), etc. This is defense-in-depth **only if the server enforces the same matrix per endpoint** — which the backend review confirms it does for every non-health route. It becomes a genuine breach if A1 leaves the server open.

**Remediation:** Pass `requirePermission` per route. Treat the server as the boundary (it is) and keep the client gate as UX.

### A9–A14 — Medium (financial correctness & API hygiene)
- **A9** `TabsEndpoints.cs:99-105`: `subtotal * TaxRate` is never rounded; settle tolerates a magic `0.005m`. Round tax and total to cents with a single money helper; replace the epsilon with `Math.Round(balance,2) > 0`.
- **A10** `LedgerEndpoints.cs:34`: cursor uses culture-sensitive `string.Compare` while `ComputeOldestUnpaid` (`MemberAccountService.cs:208-209`) uses `StringComparer.Ordinal`; under a non-invariant culture they disagree → dropped/duplicated ledger rows. Use `string.CompareOrdinal` (or real `DateTime` columns) consistently.
- **A11** Almost every list GET does `.ToListAsync()` with no paging; `/api/tabs` eager-loads all items+payments; member-overview loads the whole `TeeTimes` table and filters in memory on a JSON blob. Add cursor pagination (the ledger endpoint is the template); normalize `TeeTimePlayer`.
- **A12** No rowversion on `Product`/`PlayerTab`; stock decrement and tab edits are read-modify-write races. `reopen` (`:114-122`) will reopen a **Voided** tab with only `tabs:write`, re-enabling edits to a reversed record. Add concurrency tokens; block reopen on `Voided` and gate behind `tabs:settle`.
- **A13** Import/restore deserialize unbounded `List<T>` into memory — DoS on the Pi. Enforce Kestrel max body size + per-endpoint row caps; batch large imports.
- **A14** `InitiationFee` is client-controlled and unbounded (only negatives are blocked downstream); CRUD POST/PUT accept negative `Price`/`HourlyRate`, arbitrary `Status`/`Tier`. Introduce centralized validation (FluentValidation or endpoint filters); bound the fee against policy.

### A15–A17 — Medium (infra / correctness)
- **A15** `.github/workflows/*` use mutable action tags in a workflow with `packages: write`; a tag move could publish a malicious image. Pin actions to commit SHAs (Dependabot maintains them). *(Positive: minimal permissions, no script-injection sinks, built-in `GITHUB_TOKEN`.)*
- **A16** `deploy/pi/backup.sh:22` and the seed `import.sh` call `/api/snapshot` and `/api/import/*` with no auth header; once OIDC is enabled they silently 401 and backups break. Issue a service token/credential to automation.
- **A17** `TabsEndpoints.cs:81-85,249-252,277-280` discard `MemberAccountService` results — if a posting returns an error the tab flow commits anyway. Check `result.Error` and roll back on every call (as `LedgerEndpoints.cs:66` already does).

### A18–A25 — Low (maintainability / quality)
- **A18** `Member.Active` duplicates `Member.Status`, hand-synced in ~8 places. Make `Active` computed or drop it.
- **A19** `Program.cs:63-72` generic 500 handler does no logging; error shapes are inconsistent. Add structured logging; standardize on `ProblemDetails`.
- **A20** Pervasive stringly-typed dates/statuses/methods/tiers and JSON-blob list columns (`PlayersJson`, etc.) parsed with a swallow-all `catch`. Move to enums + `DateOnly`/`DateTimeOffset`; normalize list relations; at minimum hoist magic strings to constants.
- **A21** ~90 `<label>`s with no `htmlFor`/`id`; native `window.confirm` for destructive actions; client IDs via `Math.random` (collision-prone). Add jsx-a11y lint + label associations; standardize a confirm Modal; use `crypto.randomUUID()`.
- **A22** `NewId` copy-pasted in 5 files; ~300 LOC of near-identical CRUD; repeated import skeletons; `ClearAll` duplicated in restore. Extract a shared id generator, a generic CRUD mapper, and a generic import pipeline.
- **A23** Dev Keycloak `admin/admin` and password==username demo users (incl. `owner`) are committed but clearly dev-only and not in any prod path. Generate dev users at stack-up or source from un-committed env; add a "non-deployable" banner. Bind the dev KC admin port to `127.0.0.1`.
- **A24** CI installs .NET 8 while the project targets .NET 10 (build drift). `UseForwardedHeaders` lacks `KnownProxies` — fine behind the isolated Caddy network, but pin proxies for defense in depth.
- **A25** One giant `Store` context re-renders all consumers on the 30s poll; optimistic upserts can race the poll. Split data/actions contexts or adopt TanStack Query with ETags + `AbortController`.

---

## Remediation plan

### Phase 0 — Block release (do before any deployment)
1. **A1** — Fail closed in Production; require & uncomment OIDC config; set `ASPNETCORE_ENVIRONMENT=Production`; always validate issuer + audience.
2. **A3** — Strip ledger/status fields from `Member.Apply`; add a profile-only update DTO. *(One small change closes the biggest privilege-escalation path.)*
3. **A2** — Lock down restore/clear/reset: reconcile ledger↔balance, reject inconsistent snapshots, step-up confirm.
4. **A4** — Server-stamp `ReviewedBy` from the token; drop client free-text reviewer.
5. **A7** — Non-root container `USER`; pin the app image to an immutable tag.

### Phase 1 — Financial correctness (next sprint)
6. **A6** rowversion on `Member` (+`Product`/`PlayerTab`); **A5** money converter or cents; **A9** centralize money rounding; **A10** ordinal/`DateTime` timestamp comparison; **A17** check service results; **A12** reopen guard + concurrency. Add concurrency, rounding, and negative-input tests (current gaps).

### Phase 2 — Hardening & hygiene
7. **A8** route `requirePermission`; **A11** pagination + `TeeTimePlayer` normalization; **A13** body-size/row caps; **A14** centralized validation; **A15** SHA-pin CI actions; **A16** authenticate backup/import automation; **A23** dev-cred hygiene; **A24** align CI to .NET 10.

### Phase 3 — Maintainability
8. **A18–A22, A25** — collapse duplication, enums + typed dates, `ProblemDetails` + logging, a11y label sweep, store context split.

### Suggested test additions (currently absent)
- Concurrent balance writes (proves A6).
- Server-side money rounding without client `Math.Round` (proves A9).
- Negative/invalid CRUD payloads (proves A14).
- An auth integration test asserting Production throws when `Authority` is unset (proves A1 fix).
