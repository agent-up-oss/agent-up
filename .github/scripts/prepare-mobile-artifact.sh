#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <source-artifact> <destination-artifact>" >&2
  exit 2
fi

source_artifact="$1"
destination_artifact="$2"

hash_file() {
  local file="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$file"
  else
    shasum -a 256 "$file"
  fi
}

if [ ! -s "$source_artifact" ]; then
  echo "Missing build artifact: $source_artifact" >&2
  exit 1
fi

mkdir -p "$(dirname "$destination_artifact")"
cp -f "$source_artifact" "$destination_artifact"

checksum_file="${destination_artifact}.sha256"
(
  cd "$(dirname "$destination_artifact")"
  hash_file "$(basename "$destination_artifact")" > "$(basename "$checksum_file")"
)

size="$(wc -c < "$destination_artifact" | tr -d ' ')"
echo "Prepared $(basename "$destination_artifact") ($size bytes)"
cat "$checksum_file"
