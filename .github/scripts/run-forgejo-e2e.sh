#!/usr/bin/env bash
set -euo pipefail

# Starts no Forgejo itself: the NUnit fixture hosts a digest-pinned Forgejo
# container and runs Category=ForgejoE2E against the first-party module and the
# Server Forge slice. This is the slow ciOnly forgejo-e2e check. It is not part
# of the test-kind watchdog or the fast Server / capabilities-forgejo suites.
# Missing Docker is a failure, not a skip.

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

image="codeberg.org/forgejo/forgejo:11.0.6@sha256:71c020a9fb01593d5c9fbf2700b3b11dccc5d9e01f9a2b7dbb1d1f7ebb9cb58e"
results="artifacts/test-results/forgejo-e2e"
mkdir -p "$results"

if ! command -v docker >/dev/null 2>&1; then
  echo "Forgejo E2E requires docker on PATH." >&2
  exit 1
fi

echo "forgejo-e2e image=${image}"
docker pull "$image"

run_suite() {
  local project="$1"
  local trx="$2"
  dotnet test "$project" \
    --configuration Release \
    --results-directory "$results" \
    --settings "coverlet.runsettings" \
    --logger "trx;LogFileName=${trx}" \
    --logger "console;verbosity=detailed" \
    --filter "Category=ForgejoE2E" \
    --blame-crash \
    --blame-hang \
    --blame-hang-timeout 10m
}

run_suite "AgentUp.Capabilities.Forgejo.Tests/AgentUp.Capabilities.Forgejo.Tests.csproj" "capabilities-forgejo-e2e.trx"
run_suite "AgentUp.Server.Tests/AgentUp.Server.Tests.csproj" "server-forgejo-e2e.trx"
