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
| Tee Times     | 15-minute slot grid per course (using per-course open/close hours); book, check in, complete, cancel. |
| Members       | Roster, tiers, handicap, balance, contact info.                    |
| Courses       | Course details, rating/slope, operating hours, status.             |
| Staff         | Roster, weekly schedule grid, department coverage, and recurring weekly templates that can be applied to any week. |
| Pro Shop      | Inventory by category, stock adjustments, low-stock alerts.        |
| Player Tabs   | Open a tab per group, add inventory or custom items (auto-decrements stock), accept Cash / Card / Member Charge / Comp payments, and settle at the end of the round. |
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

All state lives under the `fairway-hq:data:v1` key in `localStorage`. The app
starts empty by default. Use **"Load sample data"** in the sidebar footer to
populate it with a demo dataset, or **"Clear all data"** to wipe everything.
