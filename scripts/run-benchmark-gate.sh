#!/usr/bin/env bash
set -euo pipefail

target="${1:?usage: run-benchmark-gate.sh <browser|server>}"
case "$target" in
  browser) project="AgentUp.Browser.Streaming.Benchmarks"; baseline="benchmarks/baselines/browser-streaming.json" ;;
  server) project="AgentUp.Server.Benchmarks"; baseline="benchmarks/baselines/server-agents.json" ;;
  *) echo "unknown benchmark target: $target" >&2; exit 2 ;;
esac
artifacts="artifacts/benchmarks/$target"
rm -rf "$artifacts"
dotnet run --configuration Release --project "$project" -- --filter '*' --exporters json --artifacts "$artifacts"
report="$(find "$artifacts/results" -name '*-report-full*.json' -print -quit)"
if [[ -z "$report" ]]; then
  echo "BenchmarkDotNet JSON report was not produced" >&2
  exit 1
fi
python3 scripts/check-benchmark-baseline.py "$report" "$baseline"
