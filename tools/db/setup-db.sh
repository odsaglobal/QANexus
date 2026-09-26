#!/usr/bin/env bash
# Sets up a fresh Postgres database for ATIP and applies all EF Core migrations.
#
# Usage:
#   tools/db/setup-db.sh            # Docker Postgres (docker-compose.yml), port 5433
#   tools/db/setup-db.sh --local    # existing local Postgres install, port 5432
#   tools/db/setup-db.sh --reset    # drop the database/volume first, then rebuild
#
# Idempotent: safe to re-run against an already-set-up environment.

set -euo pipefail

MODE="docker"
RESET=0
for arg in "$@"; do
  case "$arg" in
    --local) MODE="local" ;;
    --reset) RESET=1 ;;
    -h|--help)
      sed -n '2,10p' "$0"
      exit 0
      ;;
    *)
      echo "Unknown argument: $arg" >&2
      exit 1
      ;;
  esac
done

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$REPO_ROOT"

DB_NAME="atip"
DB_USER="atip"
DB_PASSWORD="atip"
EF_VERSION="10.0.10"

if [[ "$MODE" == "docker" ]]; then
  DB_PORT=5433
  echo "==> Starting Postgres via docker compose (host port $DB_PORT)"
  if [[ "$RESET" == "1" ]]; then
    docker compose down -v postgres 2>/dev/null || true
  fi
  docker compose up -d postgres

  echo "==> Waiting for Postgres to become healthy"
  for _ in $(seq 1 30); do
    if docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then
      break
    fi
    sleep 1
  done
  if ! docker compose exec -T postgres pg_isready -U "$DB_USER" -d "$DB_NAME" >/dev/null 2>&1; then
    echo "Postgres did not become ready in time" >&2
    exit 1
  fi
else
  DB_PORT=5432
  command -v psql >/dev/null 2>&1 || { echo "psql not found; install PostgreSQL or use docker mode" >&2; exit 1; }

  if [[ "$RESET" == "1" ]]; then
    echo "==> Dropping existing local database (if any)"
    psql postgres -c "DROP DATABASE IF EXISTS $DB_NAME;"
  fi

  echo "==> Ensuring local role/database exist (port $DB_PORT)"
  psql postgres -tAc "SELECT 1 FROM pg_roles WHERE rolname='$DB_USER'" | grep -q 1 \
    || psql postgres -c "CREATE ROLE $DB_USER LOGIN PASSWORD '$DB_PASSWORD';"
  psql postgres -tAc "SELECT 1 FROM pg_database WHERE datname='$DB_NAME'" | grep -q 1 \
    || psql postgres -c "CREATE DATABASE $DB_NAME OWNER $DB_USER;"
fi

CONNECTION_STRING="Host=localhost;Port=$DB_PORT;Database=$DB_NAME;Username=$DB_USER;Password=$DB_PASSWORD"

echo "==> Ensuring dotnet-ef $EF_VERSION is installed"
if dotnet tool list --global | grep -q "^dotnet-ef .*$EF_VERSION"; then
  : # already installed at the right version
elif dotnet tool list --global | grep -q "^dotnet-ef "; then
  dotnet tool update --global dotnet-ef --version "$EF_VERSION"
else
  dotnet tool install --global dotnet-ef --version "$EF_VERSION"
fi

echo "==> Applying EF Core migrations"
ConnectionStrings__Postgres="$CONNECTION_STRING" dotnet ef database update \
  --project src/ATIP.Infrastructure \
  --startup-project src/ATIP.Api

echo
echo "Database ready at: $CONNECTION_STRING"
if [[ "$MODE" == "docker" ]]; then
  echo "Update ConnectionStrings:Postgres in appsettings.Development.json to use Port=$DB_PORT"
  echo "(the default in appsettings.json assumes a local install on port 5432)."
fi
