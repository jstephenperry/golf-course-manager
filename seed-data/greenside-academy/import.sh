#!/usr/bin/env bash
# POST each JSON file to the corresponding /api/import endpoint in
# dependency order. Prints the per-entity result on each call.
set -euo pipefail

API_BASE="${API_BASE:-http://localhost:5210}"
DIR="$(cd "$(dirname "$0")" && pwd)"
CURL_OPTS="${CURL_OPTS:-}"

# The /api/import/* endpoints are RBAC-protected once auth is enabled.
# Provide a bearer token one of two ways:
#
#   1. Directly:   FAIRWAY_API_TOKEN=<jwt> ./import.sh
#
#   2. Via Keycloak client-credentials — set all three (+ token URL) and
#      the script fetches a fresh token itself:
#        FAIRWAY_TOKEN_URL=https://localhost:8443/auth/realms/fairway-hq/protocol/openid-connect/token
#        FAIRWAY_CLIENT_ID=fairway-import
#        FAIRWAY_CLIENT_SECRET=<secret>
#        # optional: CURL_OPTS=-k for a self-signed/internal CA
#
# Against a purely local dev API with auth disabled you can leave all of
# these unset; the script then sends no Authorization header.
AUTH_HEADER=()
token="${FAIRWAY_API_TOKEN:-}"
if [ -z "$token" ] && [ -n "${FAIRWAY_TOKEN_URL:-}" ] \
   && [ -n "${FAIRWAY_CLIENT_ID:-}" ] && [ -n "${FAIRWAY_CLIENT_SECRET:-}" ]; then
  echo "Fetching access token via client-credentials from $FAIRWAY_TOKEN_URL" >&2
  # shellcheck disable=SC2086
  token="$(curl -fsS $CURL_OPTS \
    --data-urlencode "grant_type=client_credentials" \
    --data-urlencode "client_id=${FAIRWAY_CLIENT_ID}" \
    --data-urlencode "client_secret=${FAIRWAY_CLIENT_SECRET}" \
    "$FAIRWAY_TOKEN_URL" \
    | (command -v jq >/dev/null && jq -er '.access_token' \
        || sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'))" || token=""
  if [ -z "$token" ]; then
    echo "Failed to obtain access token from $FAIRWAY_TOKEN_URL" >&2
    exit 1
  fi
fi
if [ -n "$token" ]; then
  AUTH_HEADER=(-H "Authorization: Bearer $token")
elif [[ "$API_BASE" != http://localhost:* && "$API_BASE" != http://127.0.0.1:* ]]; then
  # Non-local target almost certainly enforces auth — fail loudly rather
  # than firing off ten requests that all 401.
  echo "Refusing to import against non-local API_BASE ($API_BASE) without a token." >&2
  echo "Set FAIRWAY_API_TOKEN, or FAIRWAY_TOKEN_URL + FAIRWAY_CLIENT_ID + FAIRWAY_CLIENT_SECRET." >&2
  echo "See README.md / setup-pi.md for the service-account setup." >&2
  exit 1
fi

post() {
  local file="$1" endpoint="$2"
  printf "→ %-22s → %s\n" "$file" "$endpoint"
  # shellcheck disable=SC2086
  curl -fsS $CURL_OPTS -X POST \
    "${AUTH_HEADER[@]}" \
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
