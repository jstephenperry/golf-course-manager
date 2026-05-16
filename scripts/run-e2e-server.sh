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
export ASPNETCORE_ENVIRONMENT="Development"
exec dotnet run --no-launch-profile
