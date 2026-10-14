#!/usr/bin/env bash
set -euo pipefail

# Drives three concurrent Server autonomous loops against the in-process fake ACP
# agent until each assigned-issue queue drains. Restart must not silently resume.
# This is the slow ciOnly loop-soak check. It is not part of the test-kind
# watchdog or the fast Server suite, and it does not load a GGUF.

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

results="artifacts/test-results/loop-soak"
mkdir -p "$results"

dotnet test "AgentUp.Server.Tests/AgentUp.Server.Tests.csproj" \
  --configuration Release \
  --results-directory "$results" \
  --settings "coverlet.runsettings" \
  --logger "trx;LogFileName=loop-soak.trx" \
  --logger "console;verbosity=detailed" \
  --filter "Category=LoopSoak" \
  --blame-crash \
  --blame-hang \
  --blame-hang-timeout 5m
