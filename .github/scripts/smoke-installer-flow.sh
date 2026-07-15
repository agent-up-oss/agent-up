#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 1 ] || [ "$#" -gt 2 ]; then
  echo "Usage: $0 <platform> [payload-root]" >&2
  exit 2
fi

platform="$1"
payload_root="${2:-}"
work_dir="$(pwd)/artifacts/installer-flow-smoke/$platform"

if [ -n "$payload_root" ]; then
  if [ -n "${AGENTUP_PACKAGE_SMOKE_COMMAND:-}" ]; then
    exec "$AGENTUP_PACKAGE_SMOKE_COMMAND" validate-installer-flow "$platform" "$work_dir" "$payload_root"
  fi

  exec dotnet run --project AgentUp.PackageSmoke/AgentUp.PackageSmoke.csproj --configuration Release -- \
    validate-installer-flow "$platform" "$work_dir" "$payload_root"
else
  if [ -n "${AGENTUP_PACKAGE_SMOKE_COMMAND:-}" ]; then
    exec "$AGENTUP_PACKAGE_SMOKE_COMMAND" validate-installer-flow "$platform" "$work_dir"
  fi

  exec dotnet run --project AgentUp.PackageSmoke/AgentUp.PackageSmoke.csproj --configuration Release -- \
    validate-installer-flow "$platform" "$work_dir"
fi
