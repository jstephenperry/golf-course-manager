# Bulk import templates

The Fairway HQ app ships with no synthetic data — you provide initial state via these per-entity JSON templates.

Each `<entity>.template.json` is a small JSON array with one example row demonstrating the field shape. Drop your full dataset into a copy of that file, then upload it from the in-app **Import data…** page (or `POST` it directly to `/api/import/<entity>` with `Content-Type: application/json`).

Validation strategy: each row is checked individually. Valid rows commit; invalid rows return per-row errors in the response. A re-run with the same `id` values is safe — duplicates report `id_exists` and skip.

## Dependency order

Foreign-key validation looks at **existing** data only — it does not resolve refs against rows arriving in the same batch (or in a separate batch that hasn't been committed yet). Import in this order:

1. `nines.template.json`
2. `courses.template.json` (FK → `nines`; both `frontNineId` and `backNineId` optional)
3. `staff.template.json`
4. `products.template.json`
5. `members.template.json`
6. `tee-times.template.json` (FK → `courses`)
7. `tournaments.template.json` (FK → `courses`)
8. `maintenance.template.json` (FK → `courses`, `staff`; both optional)
9. `shifts.template.json` (FK → `staff`)
10. `weekly-templates.template.json` (FK → `staff`)

### A note on Nines

A **Nine** is a playable unit of exactly 9 holes — the building block for Courses. Each row in `nines.template.json` is self-contained: it owns its tee sets, its 9 holes (numbered 1..9), and per-tee yardages for every hole. Tee-set ids you provide are local to that row and get remapped to persisted ids on import — they only need to be unique within their parent Nine and match the `teeSetId` values used by the hole yardages.

A **Course** is then an assembled bookable round: a front Nine (required for the course to appear in tee sheets / tournaments) and an optional back Nine for an 18-hole round. Total holes, par, and yardages are derived from the referenced Nines — they're computed on the client and not stored on the Course.

## JSON Schemas

Each template has a sibling schema in `./schemas/<entity>.schema.json`. The repo's `.vscode/settings.json` wires them up so VSCode shows inline validation + autocomplete when editing a `*.template.json` file. Other editors can be configured to use the same schema files.

## Field reference

Each template's leading row includes a `_comment` field with required-vs-optional hints. The server ignores `_comment` (and any other unknown property) during import.

## What's not importable in v1

- **Player tabs** and **member applications**: lifecycle entities, not initial state. Use the app to create them.
- **Member ledger entries**: would need to reconcile with member balances and the past-due aging cache; deferred. Start with `balance: 0` for imported members and post charges via tabs or the manual Add Charge action on the member detail page.
