#!/usr/bin/env bash
set -euo pipefail

run_tier() {
  local tier="$1" budget="$2" filter="$3"; shift 3
  echo "Running $tier tier with ${budget}s wall-clock budget"
  local start=$SECONDS
  local status=0
  timeout --signal=TERM --kill-after=10s "${budget}s" bash -c '
    set -euo pipefail
    filter="$1"; shift
    for project in "$@"; do
      dotnet test "$project" --configuration Release --no-build --no-restore --settings .github/test-kind.runsettings --filter "$filter" --logger "console;verbosity=minimal"
    done
  ' _ "$filter" "$@" || status=$?
  if [[ $status -ne 0 ]]; then
    if [[ $status -eq 124 || $status -eq 137 ]]; then
      echo "$tier tier exceeded its ${budget}s wall-clock budget" >&2
    fi
    return "$status"
  fi
  echo "$tier tier completed in $((SECONDS - start))s"
}

run_tier unit 60 'FullyQualifiedName~.Unit.' AgentUp.Server.Tests AgentUp.Desktop.Tests
run_tier provider 75 'FullyQualifiedName~.Provider.' AgentUp.Server.Tests AgentUp.Desktop.Tests
run_tier e2e 180 'FullyQualifiedName~.E2E.' AgentUp.Tests
