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

mkdir -p "$BACKUP_DIR"
ts="$(date -u +%Y%m%dT%H%M%SZ)"
out="$BACKUP_DIR/fairway-snapshot-$ts.json"

# Pull the snapshot. The /api/snapshot endpoint is the same one the
# in-app "Download backup" button hits.
if ! curl -fsS "$API/api/snapshot" > "$out.partial"; then
  echo "[$ts] backup failed — could not reach $API/api/snapshot" >&2
  rm -f "$out.partial"
  exit 1
fi
mv "$out.partial" "$out"

# Prune anything older than KEEP_DAYS so the backup dir doesn't grow
# without bound. Atomically — find prints the filenames it's deleting.
find "$BACKUP_DIR" -maxdepth 1 -name 'fairway-snapshot-*.json' \
     -type f -mtime "+$KEEP_DAYS" -print -delete

echo "[$ts] wrote $out"
