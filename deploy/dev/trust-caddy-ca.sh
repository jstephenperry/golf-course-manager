#!/usr/bin/env bash
# Install Caddy's dev CA into the current user's browser trust stores
# so the dev stack at https://localhost:8443 works without warnings
# and — more importantly — so XHR/fetch calls from the SPA don't get
# silently blocked by the browser even after a cert-warning click-through.
#
# Targets:
#   - Chromium/Chrome/Brave/Edge (Linux) — shared NSS db at ~/.pki/nssdb
#   - Every Firefox profile under ~/.mozilla/firefox/
#
# This is user-level only — no sudo, no system trust store touched.
# Requires `certutil` from the nss-tools package.
#
# Re-run whenever Caddy's CA changes (e.g., after a `compose down -v`
# that nukes the caddy_data volume).
set -euo pipefail

NICKNAME="Caddy Local Authority - $(hostname)"
CERT=/tmp/caddy-dev-root.crt

if ! command -v certutil >/dev/null; then
  echo "certutil not found. Install with:"
  echo "  sudo dnf install nss-tools   # Fedora/RHEL"
  echo "  sudo apt install libnss3-tools # Debian/Ubuntu"
  exit 1
fi

if ! podman container inspect fairway-caddy >/dev/null 2>&1; then
  echo "fairway-caddy container is not running. Start the dev stack first:"
  echo "  podman compose -f deploy/dev/compose.yaml up -d"
  exit 1
fi

echo "Extracting Caddy's root CA…"
podman cp fairway-caddy:/data/caddy/pki/authorities/local/root.crt "$CERT"

install_into() {
  local db="$1"
  if [ ! -d "$db" ]; then
    echo "  - $db not found, skipping"
    return
  fi
  # Remove any previous copy of this CA before re-adding — certutil -A is
  # idempotent on the nickname, not the cert bytes, so reinstalls don't
  # accumulate.
  certutil -D -d "sql:$db" -n "$NICKNAME" >/dev/null 2>&1 || true
  certutil -A -d "sql:$db" -t "C,," -n "$NICKNAME" -i "$CERT"
  echo "  + trusted in $db"
}

echo "Installing into Chromium-family NSS db…"
mkdir -p ~/.pki/nssdb
install_into "$HOME/.pki/nssdb"

shopt -s nullglob
# Cover the common Firefox profile locations:
#   ~/.mozilla/firefox/*       — standard Linux (Debian/Ubuntu/Arch/older Fedora)
#   ~/.config/mozilla/firefox/* — newer Fedora (Firefox >= 138 with XDG layout)
#   ~/snap/firefox/...         — Snap install
#   ~/.var/app/...             — Flatpak install
firefox_profiles=(
  "$HOME/.mozilla/firefox"/*default*
  "$HOME/.mozilla/firefox"/*.default*
  "$HOME/.config/mozilla/firefox"/*default*
  "$HOME/snap/firefox/common/.mozilla/firefox"/*default*
  "$HOME/.var/app/org.mozilla.firefox/.mozilla/firefox"/*default*
)
if [ "${#firefox_profiles[@]}" -gt 0 ]; then
  echo "Installing into Firefox profiles…"
  for p in "${firefox_profiles[@]}"; do
    install_into "$p"
  done
else
  echo "(no Firefox profiles found — skipping)"
fi

echo
echo "Done. RESTART YOUR BROWSER so the new CA gets picked up."
echo "Then https://localhost:8443/ should load with a clean padlock and"
echo "fetch/XHR calls from the SPA will work without network errors."
