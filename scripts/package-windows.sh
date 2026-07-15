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

exec "$ROOT/scripts/package-release.sh" windows "$@"
