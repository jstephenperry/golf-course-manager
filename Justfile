set shell := ["bash", "-cu"]

default:
    @just --list

# ---------- Install ----------
install: install-client install-server

install-client:
    cd client && npm install

install-server:
    cd server && dotnet restore
    dotnet tool restore

# ---------- Dev loops ----------
# Run client + server in two terminals (preferred for UI iteration)
dev-client:
    cd client && npm run dev

dev-server:
    cd server/FairwayHq.Api && dotnet run

# Single-process integrated dev: build client into wwwroot, run API
dev-integrated:
    bash scripts/run-e2e-server.sh

# ---------- EF Core migrations ----------
# Generate a new migration from model changes: just migration-add <Name>
migration-add NAME:
    dotnet ef migrations add {{NAME}} --project server/FairwayHq.Api --output-dir Data/Migrations

# Apply pending migrations to the dev database
migration-update:
    dotnet ef database update --project server/FairwayHq.Api

# Show migration history
migration-list:
    dotnet ef migrations list --project server/FairwayHq.Api

# ---------- Build ----------
build: build-client build-server

build-client:
    cd client && npm run build

build-server:
    cd server && dotnet build --configuration Release

# ---------- Typecheck / lint ----------
typecheck:
    cd client && npm run typecheck

# ---------- Tests ----------
test: test-server test-client

test-server:
    cd server && dotnet test --logger "console;verbosity=minimal"

test-client:
    cd client && npm test

test-client-watch:
    cd client && npm run test:watch

coverage:
    cd client && npm run test:coverage

e2e:
    cd client && npm run e2e

e2e-install:
    cd client && npm run e2e:install

# ---------- Cleanup ----------
clean:
    rm -rf client/dist client/node_modules/.vite
    rm -rf server/FairwayHq.Api/wwwroot
    find server -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

clean-all: clean
    rm -rf client/node_modules
    rm -f server/FairwayHq.Api/fairway.db server/FairwayHq.Api/fairway.db-*

reinstall: clean-all install

# Equivalent of CI: install -> typecheck -> tests -> build
ci: install typecheck test build
