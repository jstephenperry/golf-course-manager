# ADR 0002 — Remote admin for self-hosted course installations

**Status**: Accepted (hobby-phase scope)
**Date**: 2026-05-19

## Context

The system is self-hosted at each golf course — a single Pi (or similar SBC) running the app via Podman. The deployment model is intentionally low-touch: a non-technical owner plugs in the Pi, the auto-update timer keeps the image current, and the system runs unattended.

As the number of installations grows beyond one or two, the operator (currently a single developer) needs the ability to:

- **See** what versions are deployed and whether each instance is healthy.
- **Diagnose** problems remotely — logs, ledger consistency, migration status — without asking the course owner to ssh somewhere.
- **Act** remotely — push backups, trigger updates, run database maintenance, restart the service.

The three real challenges, in order of difficulty:

1. **Reaching the device** behind small-business NAT with no IT staff and no port forwarding.
2. **Knowing what's out there** — fleet inventory, version, last check-in, health.
3. **Doing things to them** — read state, write state, run host commands.

This ADR commits to a hobby-realistic answer for all three. The goal is not enterprise fleet management. The goal is "if I deploy to four courses, I can administer them from my Fedora laptop and a $5 VPS without losing weekends."

## Goals

- A single operator can manage up to ~20 installations from one machine.
- Adding a new course is a 10-minute on-prem setup.
- No central database, queue, or message broker required to operate.
- Network plumbing is a managed-service problem, not a "build a VPN" problem.
- Every administrative action is auditable.
- Host-level operations (restart container, force image update, OS package upgrades) live OUTSIDE the application container's privilege boundary.

## Non-goals

- Multi-tenant SaaS administration of unrelated organizations.
- Real-time push notifications / WebSocket dashboards.
- A central control plane that stores fleet state.
- Multi-operator RBAC.
- Update orchestration / canary rollouts.
- Browser-based remote terminal.

## Architecture

```
                ┌──────────────────────────┐
                │  Operator's Fedora box   │
                │  - fleet-console (Vite)  │
                │  - tailscaled            │
                │  - ssh client            │
                └────────────┬─────────────┘
                             │  HTTPS / SSH over tailnet
            ┌────────────────┼────────────────┐
            │                │                │
   ┌────────▼─────┐  ┌───────▼─────┐  ┌───────▼──────┐
   │  Pi @ Pine   │  │ Pi @ Cedar  │  │ Pi @ Oak     │
   │  Grove G.C.  │  │ Hollow      │  │ Ridge        │
   │              │  │             │  │              │
   │  fairway-hq  │  │ fairway-hq  │  │ fairway-hq   │
   │  (container) │  │ (container) │  │ (container)  │
   │  /api/admin/*│  │             │  │              │
   │              │  │             │  │              │
   │  tailscaled  │  │ tailscaled  │  │ tailscaled   │
   │  (host)      │  │             │  │              │
   └──────────────┘  └─────────────┘  └──────────────┘
```

**Key properties:**

- No central server. The operator's machine *is* the control plane.
- Tailscale handles network identity, reachability through NAT, and ACL enforcement.
- The fleet console computes state on demand by polling each Pi over the tailnet. No state store to maintain or back up.
- App-level admin verbs (info, logs, backup, DB maintenance) live in the app's REST API behind a per-instance bearer token.
- Host-level admin verbs (restart container, force update, reboot) run via `tailscale ssh` from the operator's shell. Never via mounted host socket inside the container.

## Locked decisions

| Decision | Choice | Rationale |
| --- | --- | --- |
| Network reachability | **Tailscale** | Free up to 100 devices. NAT-friendly. MagicDNS. ACL JSON in repo. One-command install. No central infra for us to run. |
| Fleet console packaging | **Sibling Vite app** (`fleet-console/`) | Reuses the existing client's component primitives and types. `just fleet` runs it locally. Can be hosted on a small VPS later without changes. |
| Admin verb scope, day 1 | **Full set** (read + write, app- and host-level) | We're solving for the operator's experience, not staged trust. Audit log + idempotency keys keep blast radius contained. |
| Audit log retention | **Rolling 90 days** | Captures real-time forensic value. Long-tail history isn't useful for a hobby fleet. Avoids the audit table dominating the DB at scale. |
| Host-level verbs | **`tailscale ssh` from the operator's shell** | Keeps host privileges out of the application container. Tailscale handles auth + identity + audit on its end. Fleet console renders the SSH command for copy/paste or runs it via a local helper. |
| Identity | **Per-instance bearer token** | Compromise of one course doesn't unlock others. First-boot generation, bcrypt-hashed at rest, printed once. Rotation via `POST /admin/token/rotate`. |
| Polling model | **Fleet console pulls on-demand** | No heartbeat infrastructure required. Add cron-based polling later if alerting becomes valuable. |

## Endpoint surface (course-side)

All `/api/admin/*` routes require `Authorization: Bearer <token>`. Every authenticated request — and every 401 — writes an `AdminAuditEntry`.

| Method + path | Purpose | Risk |
| --- | --- | --- |
| `GET /admin/info` | Version, uptime, DB row counts, disk usage, last backup time | Read |
| `GET /admin/logs?lines=200` | Tail of recent app logs | Read |
| `GET /admin/health/detailed` | Migration status, ledger cache sanity, dunning last-run | Read |
| `GET /admin/audit?limit=&before=` | Paginated audit log (newest first) | Read |
| `POST /admin/backup/now` | Snapshot to disk (and optionally S3 if `BACKUP_TARGET` env is set) | Write — IO |
| `POST /admin/database/maintain` | `VACUUM`, `ANALYZE`, integrity check | Write — DB |
| `POST /admin/snapshot/upload` | Push latest snapshot to remote storage | Write — IO + egress |
| `POST /admin/token/rotate` | Issue new token, revoke current | Write — auth |

Host-level verbs are NOT endpoints. They are SSH command templates rendered by the fleet console:

```
tailscale ssh pi-pinegrove "podman restart fairway-hq"
tailscale ssh pi-pinegrove "sudo podman auto-update"
tailscale ssh pi-pinegrove "sudo reboot"
```

The fleet console either renders these as copy-to-clipboard buttons, or shells out via a local Node helper to run them on the operator's behalf. The container has zero awareness of these commands.

## Audit log shape

```
AdminAuditEntry {
    Id            : string  (aud_<hex>)
    Timestamp     : ISO 8601 UTC
    Actor         : string  (tailscale-resolved identity if available, else "token:<token-id>")
    SourceIp      : string  (tailnet IP)
    Action        : string  ("info" | "logs" | "backup.now" | "database.maintain" | ...)
    Result        : string  ("ok" | "error" | "denied")
    StatusCode    : int     (HTTP status)
    RequestSummary: string  (filtered query params or body fields — NEVER includes secrets)
    DurationMs    : int
}
```

Cleanup: a hosted service in the app, structured like `DunningService`, deletes entries with `Timestamp < UtcNow - 90 days` every six hours.

## Build plan

8 commits, in dependency order. Each commit ships green tests and is independently revertable.

| Slice | Scope |
| --- | --- |
| 1 | Admin token entity, first-boot generation, bcrypt at rest, auth middleware, 401 contract |
| 2 | `AdminAuditEntry` entity + append middleware, `GET /admin/audit` endpoint, cleanup hosted service |
| 3 | Read admin endpoints (`info`, `logs`, `health/detailed`) |
| 4 | Write admin endpoints (`backup.now`, `database.maintain`, `snapshot.upload`, `token.rotate`) — with rate limiting + idempotency keys |
| 5 | Tailscale onboarding: `setup-pi.md` updates + `deploy/tailscale/acl.hujson` |
| 6 | Fleet-console scaffold (`fleet-console/` Vite app, device registry in localStorage, `just fleet` recipe) |
| 7 | Fleet-console status + read actions (per-device polling, logs viewer, audit viewer, health drill-down) |
| 8 | Fleet-console write actions + host-level SSH command rendering |

## Open questions

1. **`/admin/logs` source.** The chiseled runtime image has no shell. Three options:
   - In-process ring buffer (last N log lines kept in memory by an `ILogger` provider). Simple, no extra deps, lost on restart.
   - Read from the container's stdout/stderr via the Podman socket from outside. Requires the fleet console to call Podman directly, not the app.
   - Persist app logs to a file under the data volume. Survives restart but adds disk IO.

   **Tentative**: in-process ring buffer (slice 3). Restart-volatile is acceptable for a hobby fleet; the audit log captures durable history of actions.

2. **Type sharing between client and fleet-console.** Sibling Vite apps in the same monorepo. Cleanest is to extract admin DTOs into a top-level `shared/` directory, but for slice-1 simplicity we'll import directly from `client/src/data/types.ts` via tsconfig path mapping and revisit if the boundary becomes awkward.

3. **Fleet-console secret storage.** Per-instance admin tokens have to live somewhere on the operator's machine. v1: `localStorage`. Acceptable on a single-operator laptop. When the console moves to a hosted VPS, swap to an encrypted-at-rest file (e.g., the user's gpg key, or a small SOPS-managed YAML).

4. **Idempotency-key scope on write endpoints.** Per-token + per-action? Per-token globally? v1: per-token + per-action, with a 24-hour TTL. Reasoning: re-firing "backup now" with the same key within 24h returns the original result rather than running another snapshot.

5. **Rate limiting policy on write endpoints.** Token-bucket per token, refilling at "10 actions/minute, burst 20." Subject to revision once usage patterns emerge.

## Alternatives considered

| Alternative | Why we didn't pick it |
| --- | --- |
| **Cloudflare Tunnel** instead of Tailscale | Works and gives free HTTPS, but requires per-Pi tunnel config and a Cloudflare account. Tailscale's identity + ACL story is a better fit for admin access; public HTTPS isn't a goal. |
| **Reverse SSH / WireGuard from scratch** | Maximum control, zero third-party dep. Significantly more operational surface and failure modes; not justified at this scale. |
| **Pi-initiated heartbeat to a central API** | Better at scale, but requires running a central API and storing fleet state. Punted to a future ADR if we deploy to >10 courses. |
| **Mount the Podman socket into the container** | Lets the app self-restart and self-update via the Podman REST API. Smuggles host privileges into the app's blast radius for marginal convenience. SSH path is cleaner. |
| **Same app with `FAIRWAY_MODE=fleet` flag** instead of a separate console | Reuses everything. Tighter coupling of release cadence. Mode-specific routes accumulate as ifs in shared components. Sibling app keeps the boundary clean. |
| **Multi-operator RBAC from day one** | Out of scope at hobby scale. When a second admin shows up, layer something — either Tailscale identity per-action or a `tokens.json` synced across operator machines. |
| **Real-time push (WebSockets, SSE) for fleet status** | Polling at 30s is fine for ≤20 devices. WebSockets add infra weight (proxy survival, reconnection logic) for no immediate payoff. |

## References

- Tailscale ACL docs: https://tailscale.com/kb/1018/acls
- Tailscale SSH: https://tailscale.com/kb/1193/tailscale-ssh
- Podman auto-update: https://docs.podman.io/en/latest/markdown/podman-auto-update.1.html
- Existing setup runbook: `deploy/pi/setup-pi.md`
- Existing image build: `Containerfile`, `.github/workflows/image.yml`
