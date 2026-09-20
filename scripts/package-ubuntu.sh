#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

if [ "$#" -lt 2 ] || [ "$#" -gt 5 ]; then
  echo "Usage: $0 <runtime-id> <version> [output-dir] [--payload-root <path>]" >&2
  exit 2
fi

if [ "${AGENTUP_PACKAGING_TARGET:-}" != "ubuntu" ] && ! command -v dpkg-deb >/dev/null 2>&1; then
  if ! command -v nix-shell >/dev/null 2>&1; then
    echo "Ubuntu packaging requires dpkg-deb or nix-shell." >&2
    exit 1
  fi
  command_line="$(printf "%q " "$0" "$@")"
  exec nix-shell "$ROOT/packaging/nix/ubuntu-package.nix" --run "$command_line"
fi

exec "$ROOT/scripts/package-release.sh" ubuntu "$@"
