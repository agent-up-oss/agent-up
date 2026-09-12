#!/usr/bin/env bash
set -uo pipefail

# Runs every *Tests.csproj except AgentUp.Tests. Continues after a failing suite so
# later projects still write TRX/Cobertura files for Test Report and coverage.

failed=0
found_tests=false

while IFS= read -r -d "" test_project; do
  test_project_name="$(basename "$(dirname "$test_project")")"
  if [ "$test_project_name" = "AgentUp.Tests" ]; then
    continue
  fi

  found_tests=true
  if ! dotnet test "$test_project" \
    --configuration Release \
    --results-directory "artifacts/test-results/$test_project_name" \
    --settings "coverlet.runsettings" \
    --logger "trx;LogFileName=test-results.trx" \
    --collect:"XPlat Code Coverage"; then
    failed=1
  fi
done < <(find . -name "*Tests.csproj" -print0 | sort -z)

if [ "$found_tests" = false ]; then
  echo "No test projects found"
  exit 1
fi

exit "$failed"
