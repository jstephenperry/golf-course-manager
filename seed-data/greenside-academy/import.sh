#!/usr/bin/env bash
# POST each JSON file to the corresponding /api/import endpoint in
# dependency order. Prints the per-entity result on each call.
set -euo pipefail

API_BASE="${API_BASE:-http://localhost:5210}"
DIR="$(cd "$(dirname "$0")" && pwd)"

post() {
  local file="$1" endpoint="$2"
  printf "→ %-22s → %s\n" "$file" "$endpoint"
  curl -fsS -X POST \
    -H "Content-Type: application/json" \
    --data-binary "@${DIR}/${file}" \
    "${API_BASE}${endpoint}" \
    | (command -v jq >/dev/null && jq -c . || cat)
  echo
}

post "nines.json"            "/api/import/nines"
post "courses.json"          "/api/import/courses"
post "staff.json"            "/api/import/staff"
post "products.json"         "/api/import/products"
post "members.json"          "/api/import/members"
post "tee-times.json"        "/api/import/tee-times"
post "tournaments.json"      "/api/import/tournaments"
post "maintenance.json"      "/api/import/maintenance"
post "shifts.json"           "/api/import/shifts"
post "weekly-templates.json" "/api/import/weekly-templates"

echo "✓ All imports submitted."
