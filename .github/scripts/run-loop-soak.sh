#!/usr/bin/env bash
set -euo pipefail

# Drains hundreds of scripted assigned issues on one Server loop and asserts
# bounded memory, handles, threads, and transcript size. Concurrency and restart
# live in AutonomousLoopIsolationTests (normal Server tier). This is the slow
# ciOnly loop-soak check. It is not part of the test-kind watchdog or the fast
# Server suite, and it does not load a GGUF.

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
