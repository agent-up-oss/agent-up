#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <version> <output-dir>" >&2
  exit 2
fi

version="$1"
output_dir="$2"
configuration="${CONFIGURATION:-Release}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
rids=(linux-x64 win-x64 osx-arm64 osx-x64)

publish_project() {
  local project="$1"
  local rid="$2"
  local destination="$3"

  dotnet publish "$project" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:IncludeAllContentForSelfExtract=true \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -p:Version="$version" \
    -o "$destination"
}

rm -rf "$output_dir"
mkdir -p "$output_dir/tools" "$output_dir/payloads"

for rid in "${rids[@]}"; do
  publish_project "$root/AgentUp.Packaging/AgentUp.Packaging.csproj" "$rid" "$output_dir/tools/$rid/packaging"
  publish_project "$root/AgentUp.PackageSmoke/AgentUp.PackageSmoke.csproj" "$rid" "$output_dir/tools/$rid/package-smoke"
  publish_project "$root/AgentUp.Desktop/AgentUp.Desktop.csproj" "$rid" "$output_dir/payloads/$rid/desktop"
  publish_project "$root/AgentUp.Server/AgentUp.Server.csproj" "$rid" "$output_dir/payloads/$rid/server"
  publish_project "$root/AgentUp.CLI/AgentUp.CLI.csproj" "$rid" "$output_dir/payloads/$rid/cli"
done

(
  cd "$output_dir"
  find . -type f -print0 | sort -z | xargs -0 sha256sum > checksums.sha256
)

cat > "$output_dir/manifest.json" <<JSON
{
  "version": "$version",
  "commit": "${GITHUB_SHA:-local}",
  "runtimes": ["linux-x64", "win-x64", "osx-arm64", "osx-x64"],
  "payloadLayout": "payloads/{rid}/{desktop,server,cli}",
  "toolLayout": "tools/{rid}/{packaging,package-smoke}",
  "checksums": "checksums.sha256"
}
JSON
