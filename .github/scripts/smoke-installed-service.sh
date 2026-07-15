#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 3 ]; then
  echo "Usage: $0 <platform> <runtime-id> <artifact-dir>" >&2
  exit 2
fi

platform="$1"
rid="$2"
artifact_dir="$3"
work_dir="$(pwd)/artifacts/service-smoke/$platform-$rid"

if [ -n "${AGENTUP_PACKAGE_SMOKE_COMMAND:-}" ]; then
  exec "$AGENTUP_PACKAGE_SMOKE_COMMAND" validate-installed-service "$platform" "$rid" "$artifact_dir" "$work_dir"
fi

exec dotnet run --project AgentUp.PackageSmoke/AgentUp.PackageSmoke.csproj --configuration Release -- \
  validate-installed-service "$platform" "$rid" "$artifact_dir" "$work_dir"
