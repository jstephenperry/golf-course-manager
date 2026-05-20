# syntax=docker/dockerfile:1.7
#
# Multi-stage build for the Fairway HQ self-hosted bundle. Produces a
# single image that serves the React SPA from the .NET API's wwwroot.
# Targeted at ARM-based SBCs (Raspberry Pi 4/5) and amd64 dev hosts.
# Build with: `podman build --platform=linux/arm64,linux/amd64 .`
# CI does the multi-arch build automatically on tag push.

# ---------- Stage 1: SPA bundle ----------
FROM --platform=$BUILDPLATFORM node:20-alpine AS spa
WORKDIR /spa
COPY client/package.json client/package-lock.json ./
# `npm install` (not `npm ci`) — npm's optional platform binaries for
# esbuild/rollup/rolldown get pruned per-host on a normal install, so
# committed lockfiles routinely miss entries that strict `ci` rejects.
# The lockfile still pins exact versions for everything that IS listed.
RUN npm install --no-audit --no-fund --prefer-offline
COPY client/ ./
# Vite bakes env vars into the bundle at build time. Pass the Keycloak
# configuration as build args so one image targets one IdP / deployment.
# A future iteration can switch to a runtime-fetched /api/auth/config
# endpoint if we want a single image to be redeployable across IdPs.
ARG VITE_KEYCLOAK_URL
ARG VITE_KEYCLOAK_REALM
ARG VITE_KEYCLOAK_CLIENT_ID
ENV VITE_KEYCLOAK_URL=$VITE_KEYCLOAK_URL \
    VITE_KEYCLOAK_REALM=$VITE_KEYCLOAK_REALM \
    VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID
RUN npm run build
# Output: /spa/dist

# ---------- Stage 2: API publish ----------
# Build is always run on the BUILDPLATFORM (CI host) with cross-compile
# via -a $TARGETARCH; that's dramatically faster than emulating a full
# ARM .NET SDK under QEMU during a multi-arch build.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS api
ARG TARGETARCH
WORKDIR /src
COPY server/FairwayHq.sln ./
COPY server/FairwayHq.Api/ ./FairwayHq.Api/
# Tests are excluded from publish but the project file lives in the
# solution; copying it satisfies `dotnet restore` on the solution graph.
COPY server/FairwayHq.Api.Tests/ ./FairwayHq.Api.Tests/
RUN dotnet restore FairwayHq.Api/FairwayHq.Api.csproj -a $TARGETARCH
RUN dotnet publish FairwayHq.Api/FairwayHq.Api.csproj \
      --no-restore \
      -c Release \
      -a $TARGETARCH \
      -o /publish \
      /p:UseAppHost=false
# Drop the built SPA into wwwroot so the API serves it as static files.
COPY --from=spa /spa/dist /publish/wwwroot

# ---------- Stage 3: runtime ----------
# chiseled-extra adds ICU + tzdata to the bare chiseled image — needed
# for currency / date formatting in non-en-US locales and for honoring
# the TZ environment variable when set by the operator.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=api /publish ./

# Connection string points at /app/data so the SQLite file lives on a
# mounted volume — survives image upgrades and container restarts. The
# operator can override via env to point at a USB SSD: e.g.
#   -e ConnectionStrings__Default='Data Source=/mnt/ssd/fairway.db'
ENV ConnectionStrings__Default="Data Source=/app/data/fairway.db" \
    ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

VOLUME ["/app/data"]
EXPOSE 8080

# Labels:
#   - autoupdate=registry tells Podman's auto-update timer to pull and
#     restart this container when a newer tag is published.
#   - opencontainers labels show up in `podman inspect` and ghcr.io UI.
LABEL io.containers.autoupdate=registry \
      org.opencontainers.image.title="Fairway HQ" \
      org.opencontainers.image.description="Self-hosted course management for small golf courses + ranges" \
      org.opencontainers.image.source="https://github.com/jstephenperry/golf-course-manager" \
      org.opencontainers.image.licenses="MIT"

ENTRYPOINT ["dotnet", "FairwayHq.Api.dll"]
