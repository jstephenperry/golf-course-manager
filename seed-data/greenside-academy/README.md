# Greenside Academy — par-3-only player-development resort

A complete test dataset for exercising the bulk-import flow. Drop these files into the `/import` UI in the order below, or run `./import.sh` for a one-shot CLI import via curl.

## Concept

- Three par-3 **Nines** built around skill progression — **The Forge** (sub-100yd wedge work), **The Crucible** (95-150yd short-iron transitions), **The Proving Ground** (140-225yd long-iron / hybrid).
- Every Nine has four tee decks: **Tour / Member / Forward / Junior**, with Junior tees as short as 40 yards. Player development is the brand.
- Three bookable **Courses** assembled from those Nines: Academy Loop (Forge + Crucible), Tournament Loop (Crucible + Proving Ground), and the walking-only Forge nine.
- Staff roster is instruction-heavy: Director of Instruction, four pros (general, junior, performance, fitting), shop staff, a greenkeeper, and a Saturday looper.
- Members are a mix of juniors, an adult comeback, a D1 commit, a corporate exec, and a beginner — the lifecycle the academy is designed for.
- Tournaments include a completed spring junior cup, an upcoming Wedge Wars scramble, a summer junior open, and the fall club championship.
- A two-day tee sheet (today + tomorrow, 2026-05-18 / 2026-05-19) plus ten historical Completed rounds so member overviews have data on first load.

## Import order

FK validation looks at existing data only, so order matters:

1. `nines.json`
2. `courses.json` (FK → nines)
3. `staff.json`
4. `products.json`
5. `members.json`
6. `tee-times.json` (FK → courses)
7. `tournaments.json` (FK → courses)
8. `maintenance.json` (FK → courses, staff)
9. `shifts.json` (FK → staff)
10. `weekly-templates.json` (FK → staff)

## One-shot import

With the API running on `http://localhost:5210`:

```bash
./import.sh                 # uses default API base
API_BASE=http://localhost:5210 ./import.sh   # explicit
```

The script POSTs each file in order and echoes the `{created, skipped, errors}` summary per entity.

### Authentication

`/api/import/*` is RBAC-protected. Against a **local** (`localhost`/`127.0.0.1`) dev API with auth disabled, no token is needed. Against **any other** `API_BASE` the script refuses to run without one — supply it either way:

```bash
# Direct token
FAIRWAY_API_TOKEN=<jwt> API_BASE=https://fairway.local:8443 ./import.sh

# Or Keycloak client-credentials (script fetches a fresh token itself)
FAIRWAY_TOKEN_URL=https://fairway.local:8443/auth/realms/fairway-hq/protocol/openid-connect/token \
FAIRWAY_CLIENT_ID=fairway-import FAIRWAY_CLIENT_SECRET=<secret> \
CURL_OPTS=-k API_BASE=https://fairway.local:8443 ./import.sh
```

The service-account client must hold a role permitted to call the import endpoints. `CURL_OPTS=-k` is for Caddy's internal-CA LAN mode.

## Re-running

Imports are idempotent — re-uploading a file with the same `id` values reports `id_exists` and skips. To start fresh, hit the **Clear all data** button in the sidebar (or `POST /api/clear`) before re-running.
