# Fairway HQ — Raspberry Pi deployment

A complete walk-through to take a Raspberry Pi 4 / 5 from "in the box" to running Fairway HQ for a single course, with auto-updates and nightly backups.

## What you need

| Item | Notes |
| --- | --- |
| Raspberry Pi 4 (4GB+) or Pi 5 (4GB+) | Pi 5 strongly preferred for NVMe boot |
| USB SSD or NVMe HAT + SSD | **Do not run on the SD card long-term** — they die. Use SD only for OS boot. |
| Power supply | Official Pi PSU; underpowered PSUs cause silent corruption |
| Mini-UPS (optional but recommended) | $30 buys you graceful shutdown on a rural power blip |
| Static IP on the WiFi router | DHCP reservation against the Pi's MAC. Staff phones need a stable address. |

## 1. Flash + first boot

Use **Raspberry Pi Imager** to flash **Raspberry Pi OS Lite (64-bit)** — *not* the desktop variant. In the Imager's settings (gear icon) set hostname (`fairway`), username, SSH on, and your WiFi. First boot is headless.

```bash
ssh <user>@fairway.local
sudo apt update && sudo apt full-upgrade -y
sudo apt install -y podman podman-compose curl wget
```

If your distro's Podman is older than 4.4, also install the `podman-plugins` package so `podman compose` works as expected.

## 2. (Recommended) Boot from SSD

On the Pi 5, install the NVMe HAT and a small NVMe SSD. Re-flash Raspberry Pi OS directly to the NVMe via the Imager, then update the boot order:

```bash
sudo raspi-config   # Advanced Options → Boot Order → NVMe/USB Boot
sudo shutdown -h now
```

Yank the SD card, power up. The Pi now runs entirely off the SSD; SD card death is no longer your weekly emergency.

## 3. Run the app

The compose file pulls two images: the app itself and **Caddy** as a reverse proxy. Caddy is the only thing that listens on the LAN (port 443 for TLS, 8443 if you're using its internal CA in LAN-only mode); the app container is reachable only on the internal compose network.

```bash
sudo mkdir -p /opt/fairway-hq
sudo curl -fsSL https://raw.githubusercontent.com/jstephenperry/golf-course-manager/main/deploy/pi/compose.yaml \
     -o /opt/fairway-hq/compose.yaml
sudo curl -fsSL https://raw.githubusercontent.com/jstephenperry/golf-course-manager/main/deploy/pi/Caddyfile \
     -o /opt/fairway-hq/Caddyfile

# Edit the TZ value in compose.yaml to match the course's local time zone.
# Edit the hostname in Caddyfile if you're using a domain other than fairway.local.
sudo nano /opt/fairway-hq/compose.yaml
sudo nano /opt/fairway-hq/Caddyfile
```

**Authentication is required in production.** The app runs with
`ASPNETCORE_ENVIRONMENT=Production` and refuses to start unless the two
OIDC variables are set — there is no unauthenticated prod mode. Provide
them in an `.env` file next to the compose file. `podman compose` /
`docker compose` auto-load `.env` from the compose directory:

```bash
sudo tee /opt/fairway-hq/.env >/dev/null <<'EOF'
# OIDC issuer (token `iss`) — the realm URL behind Caddy's /auth route.
FAIRWAY_AUTH_AUTHORITY=https://fairway.local:8443/auth/realms/fairway-hq
# OIDC audience — the SPA client id registered in the realm.
FAIRWAY_AUTH_AUDIENCE=fairway-hq-spa
EOF
sudo chmod 600 /opt/fairway-hq/.env
```

If either variable is missing or empty, `compose up` fails fast with a
clear error (the `${VAR:?...}` guard in compose.yaml) rather than booting
an open instance. You must also wire Caddy's `/auth/*` route to your IdP
(see `Caddyfile`) so the issuer above is reachable.

```bash
cd /opt/fairway-hq
sudo podman compose pull
sudo podman compose up -d
```

Verify:

```bash
# Caddy responds — TLS handshake should work even with the self-signed cert
curl -k https://fairway.local:8443/api/health
# {"status":"ok","time":"…"}
```

From a staff device on the same WiFi: `https://fairway.local:8443/`

### Trust Caddy's CA on staff devices (LAN-only / mode 1)

In the LAN-only default mode, Caddy signs the TLS cert from a private CA. Devices need to trust that CA once, or browsers will warn on every visit.

```bash
# Extract the root cert
sudo podman cp fairway-caddy:/data/caddy/pki/authorities/local/root.crt /tmp/fairway-root.crt

# Distribute /tmp/fairway-root.crt to each staff device. Trust install:
#   - iOS: AirDrop the .crt, then Settings → General → VPN & Device Management → install profile, then Settings → General → About → Certificate Trust Settings → enable.
#   - Android: Settings → Security → Encryption & credentials → Install a certificate → CA certificate.
#   - macOS:  open the .crt → Keychain Access → set "Always Trust".
#   - Windows: double-click → Install → Local Machine → Trusted Root Certification Authorities.
#   - Linux (Fedora): sudo trust anchor fairway-root.crt
```

Alternatively, use **mode 2** in `Caddyfile`: a public DNS name + Let's Encrypt cert. No per-device install, but it requires port-forwarding 80/443 to the Pi and a domain you control. See the comments in `Caddyfile` for the switch.

## 4. Auto-updates (weekly)

The image is tagged with `io.containers.autoupdate=registry`. Enable the system timer that polls ghcr.io for newer images and restarts the service:

```bash
sudo systemctl enable --now podman-auto-update.timer
systemctl list-timers podman-auto-update.timer
```

By default the timer fires once per day. To run it manually for testing:

```bash
sudo podman auto-update
```

If you don't want automatic updates, **don't** enable the timer. Updates then happen only when you run `podman compose pull && podman compose up -d` manually.

## 5. Nightly backups

```bash
sudo curl -fsSL https://raw.githubusercontent.com/jstephenperry/golf-course-manager/main/deploy/pi/backup.sh \
     -o /opt/fairway-hq/backup.sh
sudo chmod +x /opt/fairway-hq/backup.sh

# If you have a USB stick mounted at /mnt/usb, point backups there.
# Otherwise this writes to /var/backups/fairway-hq on the Pi itself.
sudo crontab -e
```

`/api/snapshot` is RBAC-protected (auth is required in prod), so the
backup job needs a bearer token. Create a dedicated Keycloak
**service-account / client-credentials** client (e.g. `fairway-backup`)
with the role `/api/snapshot` requires, then let the script fetch a fresh
token each run — no long-lived JWT on disk. Put the client secret in the
backup env file:

```bash
sudo tee /opt/fairway-hq/backup.env >/dev/null <<'EOF'
FAIRWAY_TOKEN_URL=https://fairway.local:8443/auth/realms/fairway-hq/protocol/openid-connect/token
FAIRWAY_CLIENT_ID=fairway-backup
FAIRWAY_CLIENT_SECRET=CHANGE_ME
# LAN-only mode uses Caddy's internal CA; -k skips host CA trust for this call.
CURL_OPTS=-k
EOF
sudo chmod 600 /opt/fairway-hq/backup.env
```

Alternatively, skip the client and hand the script a token directly with
`FAIRWAY_API_TOKEN=<jwt>` (handy for one-off manual runs). If neither is
provided the script exits non-zero with a clear message rather than
writing a `401` body to a backup file.

Add (note the `set -a` so the env file's vars are exported to the script):

```
0 2 * * *  set -a; . /opt/fairway-hq/backup.env; BACKUP_DIR=/mnt/usb/fairway-backups /opt/fairway-hq/backup.sh >> /var/log/fairway-backup.log 2>&1
```

Test it manually:

```bash
sudo bash -c 'set -a; . /opt/fairway-hq/backup.env; /opt/fairway-hq/backup.sh'
ls -la /var/backups/fairway-hq/
```

For off-site backup, periodically rsync that directory to a cheap S3-compatible bucket (Backblaze B2, Wasabi, etc.). One line in cron.

## 6. Initial data

With the app running and no data, visit `https://fairway.local:8443/import` from a laptop. Upload the per-entity template JSON files in dependency order (see `client/public/templates/README.md`). Or run the `seed-data/greenside-academy/import.sh` against `API_BASE=https://fairway.local:8443` for a complete demo dataset.

The `/api/import/*` endpoints are RBAC-protected, so against a non-local `API_BASE` the script requires a token — same options as the backup job (`FAIRWAY_API_TOKEN`, or `FAIRWAY_TOKEN_URL` + `FAIRWAY_CLIENT_ID` + `FAIRWAY_CLIENT_SECRET`). For the internal-CA LAN mode add `CURL_OPTS=-k`:

```bash
FAIRWAY_API_TOKEN=<jwt> CURL_OPTS=-k API_BASE=https://fairway.local:8443 \
  seed-data/greenside-academy/import.sh
```

## Troubleshooting

- **`curl /api/health` returns nothing** — container probably hasn't finished starting. Check `sudo podman logs -f fairway-hq`.
- **Time zone wrong on Dashboard** — the `TZ` env var in compose.yaml is what the API reads. Edit it, then `podman compose up -d` to recreate.
- **Image won't pull** — ghcr.io is rate-limited for anonymous pulls (~100/hr). If you're rebuilding repeatedly, log in: `podman login ghcr.io`.
- **SQLite says database is locked** — extremely rare on this app; if it happens, restart the container. WAL mode is enabled and the API is single-process.
- **Pi WiFi flaky** — wire it. Ethernet to the router is dramatically more reliable than 2.4GHz from across a pro shop.

## Hardware bill of materials

| Item | Approx cost (USD) |
| --- | --- |
| Raspberry Pi 5, 4GB | $60 |
| 256GB NVMe SSD + Pi 5 NVMe HAT | $45 |
| Official 27W Pi 5 PSU | $12 |
| Case + small fan | $15 |
| Mini-UPS HAT (optional) | $30 |
| **Total** | **~$160** |

That's the whole on-prem hardware bill for one course. Software is whatever you charge.
