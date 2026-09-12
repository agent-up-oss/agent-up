#!/usr/bin/env bash
set -uo pipefail

# Runs every *Tests.csproj except AgentUp.Tests. Continues after a failing suite so
# later projects still write TRX/Cobertura files for Test Report and coverage.
# Set COLLECT_COVERAGE=0 when this job only needs TRX output.

failed=0
found_tests=false
collect_coverage="${COLLECT_COVERAGE:-1}"

while IFS= read -r -d "" test_project; do
  test_project_name="$(basename "$(dirname "$test_project")")"
  if [ "$test_project_name" = "AgentUp.Tests" ]; then
    continue
  fi

  found_tests=true
  test_command=(
    dotnet test "$test_project"
    --configuration Release
    --results-directory "artifacts/test-results/$test_project_name"
    --logger "trx;LogFileName=test-results.trx"
  )
  if [ "$collect_coverage" = "1" ]; then
    test_command+=(--settings "coverlet.runsettings" --collect:"XPlat Code Coverage")
  fi
  if ! "${test_command[@]}"; then
    failed=1
  fi
done < <(find . -name "*Tests.csproj" -print0 | sort -z)

if [ "$found_tests" = false ]; then
  echo "No test projects found"
  exit 1
fi

exit "$failed"
