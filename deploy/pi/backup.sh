#!/usr/bin/env bash
# Pull a JSON snapshot from the running Fairway HQ container and write
# it to a dated file. Designed to be run from cron on the Pi:
#
#   # Daily at 2am — drop to USB SSD mounted at /mnt/usb
#   0 2 * * *  BACKUP_DIR=/mnt/usb/fairway-backups /opt/fairway-hq/backup.sh >> /var/log/fairway-backup.log 2>&1
#
# Or, if there's no attached storage, write to the Pi's filesystem and
# rsync up to a cloud bucket from a separate job.
set -euo pipefail

API="${API:-http://localhost:8080}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/fairway-hq}"
KEEP_DAYS="${KEEP_DAYS:-30}"

# /api/snapshot is RBAC-protected once auth is enabled (it is, in prod).
# Provide a bearer token one of two ways:
#
#   1. Directly:   FAIRWAY_API_TOKEN=<jwt> /opt/fairway-hq/backup.sh
#
#   2. Via Keycloak client-credentials — set all four and the script
#      fetches a fresh token itself (no long-lived JWT on disk):
#        FAIRWAY_TOKEN_URL=https://fairway.local:8443/auth/realms/fairway-hq/protocol/openid-connect/token
#        FAIRWAY_CLIENT_ID=fairway-backup
#        FAIRWAY_CLIENT_SECRET=<secret>
#        # optional: FAIRWAY_TOKEN_SCOPE / extra curl opts via CURL_OPTS (e.g. -k for internal CA)
#
# The service-account client must hold whatever role /api/snapshot requires.
CURL_OPTS="${CURL_OPTS:-}"

ts="$(date -u +%Y%m%dT%H%M%SZ)"

token="${FAIRWAY_API_TOKEN:-}"
if [ -z "$token" ]; then
  if [ -n "${FAIRWAY_TOKEN_URL:-}" ] && [ -n "${FAIRWAY_CLIENT_ID:-}" ] && [ -n "${FAIRWAY_CLIENT_SECRET:-}" ]; then
    echo "[$ts] fetching access token via client-credentials from $FAIRWAY_TOKEN_URL" >&2
    # shellcheck disable=SC2086
    token="$(curl -fsS $CURL_OPTS \
      --data-urlencode "grant_type=client_credentials" \
      --data-urlencode "client_id=${FAIRWAY_CLIENT_ID}" \
      --data-urlencode "client_secret=${FAIRWAY_CLIENT_SECRET}" \
      ${FAIRWAY_TOKEN_SCOPE:+--data-urlencode "scope=${FAIRWAY_TOKEN_SCOPE}"} \
      "$FAIRWAY_TOKEN_URL" \
      | (command -v jq >/dev/null && jq -er '.access_token' \
          || sed -n 's/.*"access_token":"\([^"]*\)".*/\1/p'))" || token=""
    if [ -z "$token" ]; then
      echo "[$ts] backup failed — could not obtain access token from $FAIRWAY_TOKEN_URL" >&2
      exit 1
    fi
  else
    echo "[$ts] backup failed — no credentials. Set FAIRWAY_API_TOKEN, or" >&2
    echo "       FAIRWAY_TOKEN_URL + FAIRWAY_CLIENT_ID + FAIRWAY_CLIENT_SECRET." >&2
    echo "       /api/snapshot requires auth in production. See setup-pi.md §5." >&2
    exit 1
  fi
fi

mkdir -p "$BACKUP_DIR"
out="$BACKUP_DIR/fairway-snapshot-$ts.json"

# Pull the snapshot. The /api/snapshot endpoint is the same one the
# in-app "Download backup" button hits.
# shellcheck disable=SC2086
if ! curl -fsS $CURL_OPTS -H "Authorization: Bearer $token" "$API/api/snapshot" > "$out.partial"; then
  echo "[$ts] backup failed — could not reach $API/api/snapshot (token expired or unauthorized?)" >&2
  rm -f "$out.partial"
  exit 1
fi
mv "$out.partial" "$out"

# Prune anything older than KEEP_DAYS so the backup dir doesn't grow
# without bound. Atomically — find prints the filenames it's deleting.
find "$BACKUP_DIR" -maxdepth 1 -name 'fairway-snapshot-*.json' \
     -type f -mtime "+$KEEP_DAYS" -print -delete

echo "[$ts] wrote $out"
