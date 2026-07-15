#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
  echo "Usage: $0 <runtime-id> <version> [output-dir]" >&2
  exit 2
fi

if [ "${AGENTUP_PACKAGING_TARGET:-}" != "windows" ]; then
  command_line="$(printf "%q " "$0" "$@")"
  exec nix-shell "$ROOT/packaging/nix/windows-package.nix" --run "$command_line"
fi

if ! command -v wix >/dev/null 2>&1; then
  echo "WiX CLI 'wix' is required for Windows packaging." >&2
  echo "The Windows Nix wrapper should restore the pinned WiX .NET tool from packaging/windows/dotnet-tools.json." >&2
  exit 127
fi

exec dotnet run --project "$ROOT/AgentUp.Packaging/AgentUp.Packaging.csproj" --configuration "${CONFIGURATION:-Release}" -- \
  package windows "$@"
