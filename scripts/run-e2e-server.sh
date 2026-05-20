#!/usr/bin/env bash
# Builds the React client, copies the bundle into the API's wwwroot,
# resets the database, and starts the API on a fixed port for E2E tests.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLIENT_DIR="$REPO_ROOT/client"
API_DIR="$REPO_ROOT/server/FairwayHq.Api"
WWWROOT="$API_DIR/wwwroot"

echo "[e2e] Building client…"
(cd "$CLIENT_DIR" && npm run build >/dev/null)

echo "[e2e] Copying bundle to $WWWROOT"
rm -rf "$WWWROOT"
cp -R "$CLIENT_DIR/dist" "$WWWROOT"

echo "[e2e] Resetting dev DB"
rm -f "$API_DIR"/fairway.db*

echo "[e2e] Starting API on http://localhost:5210"
cd "$API_DIR"
export ASPNETCORE_URLS="http://localhost:5210"
# Testing (not Development) so the API uses TestAuthHandler: with no auth
# header, requests are synthesized as an `owner` (same mechanism as the
# integration suite). Development requires a real Keycloak JWT on every
# /api/* call (default-deny RBAC FallbackPolicy), which E2E can't provide
# — that produced uniform 401s and every spec failing.
export ASPNETCORE_ENVIRONMENT="Testing"
exec dotnet run --no-launch-profile
