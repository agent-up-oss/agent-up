#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "Usage: $0 <platform>" >&2
  exit 2
fi

platform="$1"
work_dir="$(pwd)/artifacts/installer-flow-smoke/$platform"

exec dotnet run --project AgentUp.PackageSmoke/AgentUp.PackageSmoke.csproj --configuration Release -- \
  validate-installer-flow "$platform" "$work_dir"
