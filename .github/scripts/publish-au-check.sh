#!/usr/bin/env bash
set -euo pipefail

output_dir="${1:-artifacts/au-check}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
configuration="${CONFIGURATION:-Release}"

rm -rf "$output_dir"
mkdir -p "$output_dir"

rids=(linux-x64 linux-arm64 osx-arm64 osx-x64 win-x64)
for rid in "${rids[@]}"; do
  destination="$(mktemp -d)"
  dotnet publish "$root/AgentUp.AUCheck/AgentUp.AUCheck.csproj" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:IncludeAllContentForSelfExtract=true \
    -p:PublishTrimmed=false \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -o "$destination"

  if [ "$rid" = "win-x64" ]; then
    mv "$destination/au-check.exe" "$output_dir/au-check-win-x64.exe"
  else
    mv "$destination/au-check" "$output_dir/au-check-$rid"
    chmod +x "$output_dir/au-check-$rid"
  fi

  rm -rf "$destination"
done
