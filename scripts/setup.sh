#!/usr/bin/env bash
# One-command environment setup for the DevOpsLab starter project (macOS / Linux / WSL).
# Usage:  bash scripts/setup.sh
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

API="src/DevOpsLab.WebApi"
INFRA="src/DevOpsLab.Infrastructure"

step()  { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }
ok()    { printf '\033[1;32m  OK  \033[0m%s\n' "$*"; }
die()   { printf '\033[1;31m  FAIL\033[0m %s\n' "$*" >&2; exit 1; }

step "1/7  Checking prerequisites"
command -v dotnet >/dev/null 2>&1 || die ".NET SDK not found. Install .NET 8 SDK and re-run."
command -v docker >/dev/null 2>&1 || die "Docker not found. Install Docker Desktop (or Docker Engine) and re-run."
docker info >/dev/null 2>&1 || die "Docker is installed but not running. Start Docker Desktop and re-run."

SDK_VERSION="$(dotnet --version)"
case "$SDK_VERSION" in
  8.*) ok ".NET SDK $SDK_VERSION" ;;
  *)   die ".NET 8 SDK required, found $SDK_VERSION" ;;
esac
ok "Docker is running"

step "2/7  Preparing local configuration"
if [ ! -f .env ]; then
  cp .env.example .env
  ok "Created .env from .env.example"
else
  ok ".env already exists, leaving it untouched"
fi

step "3/7  Starting SQL Server"
docker compose up -d
printf '  waiting for the database to report healthy'
for _ in $(seq 1 60); do
  STATUS="$(docker inspect --format '{{.State.Health.Status}}' devopslab-sqlserver 2>/dev/null || echo starting)"
  [ "$STATUS" = "healthy" ] && break
  printf '.'
  sleep 3
done
printf '\n'
[ "${STATUS:-}" = "healthy" ] || die "SQL Server did not become healthy in time. Check: docker compose logs sqlserver"
ok "SQL Server is healthy"

step "4/7  Restoring and building the solution"
dotnet restore DevOpsLab.sln
dotnet build DevOpsLab.sln -c Debug --no-restore
ok "Build succeeded"

step "5/7  Preparing database migrations"
if ! dotnet ef --version >/dev/null 2>&1; then
  dotnet tool install --global dotnet-ef >/dev/null 2>&1 || dotnet tool update --global dotnet-ef >/dev/null 2>&1
  export PATH="$PATH:$HOME/.dotnet/tools"
fi
if [ ! -d "$INFRA/Migrations" ]; then
  dotnet ef migrations add InitialCreate --project "$INFRA" --startup-project "$API"
  ok "Created the InitialCreate migration"
else
  ok "Migrations folder already present"
fi
dotnet ef database update --project "$INFRA" --startup-project "$API"
ok "Database schema is up to date"

step "6/7  Running the test suite"
dotnet test DevOpsLab.sln -c Debug --no-build
ok "All tests passed"

step "7/7  Done"
cat <<'MSG'

  Start the API with:   make run        (or: dotnet run --project src/DevOpsLab.WebApi)

  Then check:
    http://localhost:5080/healthz/live    liveness  - is the process alive?
    http://localhost:5080/healthz/ready   readiness - can it actually serve traffic?
    http://localhost:5080/swagger         API explorer

  Reminder: a process that is alive is NOT necessarily ready. You will rely on
  that difference in Session 11 when you switch traffic with zero downtime.

MSG
