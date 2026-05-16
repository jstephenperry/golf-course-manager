# Fairway HQ — Golf Course Manager

End-to-end operations app for a golf course: tee times, members, courses, staff
scheduling, pro shop inventory, player tabs, tournaments, and maintenance.

## Architecture

| Layer  | Stack |
| ------ | ----- |
| Client | React 18 + TypeScript + Vite, React Router 6, code-split routes, in-tree toast + error boundary |
| Server | ASP.NET Core 8 (minimal APIs), EF Core 8 with SQLite |
| Shared | JSON DTOs over `/api`, server is the single source of truth for stock, member balances, and tab state |
| Tests  | xUnit + `WebApplicationFactory` (server), Vitest + React Testing Library (client), Playwright (E2E) |
| CI     | GitHub Actions: `server`, `client`, `e2e` jobs |

```
golf-course-manager/
├── client/        # React SPA
├── server/        # .NET solution (FairwayHq.Api + FairwayHq.Api.Tests)
├── scripts/       # Dev orchestration (run-e2e-server.sh)
└── .github/workflows/ci.yml
```

In production the API serves the built SPA from `wwwroot/` and the API on
`/api/*` — one deployable.

## Prerequisites

- Node.js 20+
- .NET 8 SDK
- (Optional) Playwright browsers (`cd client && npx playwright install chromium`)

## Run it locally

Two-process dev loop (recommended while iterating on UI):

```bash
# Terminal 1 — API on :5210
cd server/FairwayHq.Api
dotnet run

# Terminal 2 — Vite dev server on :5173, proxies /api to :5210
cd client
npm install
npm run dev
```

Open <http://localhost:5173>.

Single-process integrated run (matches production):

```bash
bash scripts/run-e2e-server.sh   # builds client, copies into wwwroot, starts API
# open http://localhost:5210
```

## API surface

All endpoints under `/api`. Swagger UI at `/swagger` in development.

| Method | Path | Purpose |
| ------ | ---- | ------- |
| GET    | `/api/health` | Liveness |
| CRUD   | `/api/members`, `/api/courses`, `/api/tee-times`, `/api/staff`, `/api/shifts`, `/api/weekly-templates`, `/api/products`, `/api/tournaments`, `/api/maintenance` | Standard list / create / update / delete |
| POST   | `/api/products/{id}/adjust-stock` | Atomic stock delta |
| CRUD   | `/api/tabs`, `/api/tabs/{id}` | List, create, update meta |
| POST   | `/api/tabs/{id}/items`, `PUT /api/tabs/{id}/items/{itemId}/quantity`, `DELETE /api/tabs/{id}/items/{itemId}` | Tab line items — server snapshots product price + adjusts stock transactionally |
| POST   | `/api/tabs/{id}/payments`, `DELETE /api/tabs/{id}/payments/{paymentId}` | Payments — Member Charge posts to member balance; reversed on delete or void |
| POST   | `/api/tabs/{id}/settle`, `/api/tabs/{id}/void`, `/api/tabs/{id}/reopen` | Lifecycle; `settle` 400s if balance > 0; `void` reverses stock + charges |
| GET    | `/api/snapshot` | Full DB JSON for backup/export |
| POST   | `/api/snapshot/restore` | Replace all data with a snapshot |
| POST   | `/api/reset` | Wipe + reseed |
| POST   | `/api/clear` | Wipe |

## Testing

```bash
# Server: 9 integration tests using WebApplicationFactory + in-memory SQLite
cd server && dotnet test

# Client unit/component tests
cd client && npm test          # one-shot
npm run test:watch             # watch mode
npm run test:coverage          # with V8 coverage

# Full end-to-end (boots API + serves built SPA, then drives Chromium)
cd client && npm run e2e:install   # once, downloads Chromium
npm run e2e
```

The unit tests cover the API client (fetch wrapper + error translation),
the toaster lifecycle, and the date / time / slot / tab-math helpers.
The integration tests cover member CRUD, the full tab lifecycle (open →
add item → pay → settle), void-with-rollback, settled-tab immutability,
and snapshot/restore round-trip.

## Modules (UI)

| Module        | What it does                                                       |
| ------------- | ------------------------------------------------------------------ |
| Dashboard     | Today's tee sheet, on-duty crew, restock alerts, open-tab balance. |
| Tee Times     | 15-minute slot grid per course; book, check in, complete, open tab. |
| Members       | Roster, tiers, handicap, balance, contact info.                    |
| Courses       | Course details, rating/slope, operating hours, status.             |
| Staff         | Roster, weekly schedule grid, department coverage, recurring weekly templates. |
| Pro Shop      | Inventory by category, atomic stock adjustments, low-stock alerts. |
| Player Tabs   | Open per-group tabs, add inventory items (server-side stock decrement), accept Cash / Card / Member Charge / Comp, settle at zero balance, void with full rollback. |
| Tournaments   | Schedule events, manage registrations, format & entry fee.         |
| Maintenance   | Task list with priority, assignment, due date, and status workflow.|

## Data lifecycle

- The database is created automatically on first server start
  (`fairway.db` in the API project directory) and seeded with demo data.
- The sidebar exposes **Load sample data** (`POST /api/reset`),
  **Download backup** (`GET /api/snapshot` → JSON file),
  **Restore from backup…** (file upload → `POST /api/snapshot/restore`),
  and **Clear all data** (`POST /api/clear`).

## Production deployment notes

- The API builds a self-contained `wwwroot/` from the client's `dist/` —
  see `scripts/run-e2e-server.sh` for the exact recipe.
- Connection string is read from `ConnectionStrings:Default` (env var
  `ConnectionStrings__Default` in containers), defaulting to a local
  SQLite file. Swap to Postgres/SqlServer by changing the provider in
  `Program.cs` and adding the appropriate EF package.
- CORS is open to `localhost:5173` in Development only; production
  hosts the SPA same-origin, so CORS is moot.
