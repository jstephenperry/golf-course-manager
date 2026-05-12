# Fairway HQ — Golf Course Manager

A single-page React application for managing all facets of a golf course
operation: tee times, members, courses, staff scheduling, pro shop inventory,
tournaments, and maintenance.

The app runs entirely in the browser and persists state to `localStorage`, so
it's a self-contained demo with no backend required.

## Stack

- React 18 + TypeScript
- Vite
- React Router v6
- Plain CSS (no UI framework)

## Modules

| Module        | What it does                                                       |
| ------------- | ------------------------------------------------------------------ |
| Dashboard     | Today's tee sheet, active shifts, restock alerts, upcoming events. |
| Tee Times     | Book / edit / cancel tee times, check players in, mark completed.  |
| Members       | Roster, tiers, handicap, balance, contact info.                    |
| Courses       | Course details, rating/slope, status (Open / Cart Path Only / Closed). |
| Staff         | Roster plus a daily shift schedule.                                |
| Pro Shop      | Inventory by category, stock adjustments, low-stock alerts.        |
| Tournaments   | Schedule events, manage registrations, format & entry fee.         |
| Maintenance   | Task list with priority, assignment, due date and status workflow. |

## Getting started

```bash
npm install
npm run dev
```

Open the URL Vite prints (default <http://localhost:5173>).

### Other scripts

```bash
npm run build      # type-check and produce a production bundle in dist/
npm run preview    # serve the built bundle locally
npm run typecheck  # type-check only
```

## Data

All state lives under the `fairway-hq:data:v1` key in `localStorage`. Use the
**"Reset demo data"** button in the sidebar footer to wipe local edits and
restore the seed fixtures.
