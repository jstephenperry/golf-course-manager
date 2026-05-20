---
description: Rebuild + redeploy the containerized dev stack after code changes
---

The dev stack at `https://localhost:8443` (compose in `deploy/dev/`) does **not**
hot-reload. Edits under `client/` or `server/` are baked into the `fairway-hq`
image at build time (Vite bakes `VITE_KEYCLOAK_*` as build args), so the running
container keeps serving the OLD bundle until the image is rebuilt and the
container recreated. A stale image is the #1 cause of "my fix didn't work."

Exceptions that do NOT need an image rebuild (mounted volumes / env only):
- `deploy/dev/Caddyfile` → `podman compose -f deploy/dev/compose.yaml restart caddy`
- compose `environment:` / Keycloak realm import → `restart` the relevant service

## Steps

1. Make sure code typechecks/tests first (cheap, catches errors before a slow build):
   - `cd client && npm run typecheck && npm test`
   - `cd server && dotnet test`
2. Rebuild and recreate just the app container:
   - `cd deploy/dev && podman compose build fairway-hq && podman compose up -d fairway-hq`
3. Wait for health, then confirm the NEW bundle is being served (hash should change):
   - poll `podman inspect fairway-hq --format '{{.State.Health.Status}}'` until `healthy`
   - `curl -sk https://localhost:8443/ | grep -o 'main-[A-Za-z0-9_-]*\.js'`
4. Report the new bundle hash so the user knows the rebuild actually took effect.

If the user reports a change "not working," verify the served bundle hash changed
before debugging the code — you may just be looking at a stale image.

$ARGUMENTS
