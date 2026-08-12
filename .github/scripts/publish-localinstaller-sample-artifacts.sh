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

restore_runtime() {
  local rid="$1"

  dotnet restore "$root/localinstaller.sln" \
    --runtime "$rid"
}

publish_project() {
  local project="$1"
  local rid="$2"
  local destination="$3"

  dotnet publish "$project" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --no-restore \
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
  restore_runtime "$rid"
  publish_project "$root/LocalInstaller.Sample.Packager/LocalInstaller.Sample.Packager.csproj" "$rid" "$output_dir/tools/$rid/packager"
  publish_project "$root/LocalInstaller.Sample.Smoke/LocalInstaller.Sample.Smoke.csproj" "$rid" "$output_dir/tools/$rid/smoke"
  publish_project "$root/LocalInstaller.Sample.InstallerApp/LocalInstaller.Sample.InstallerApp.csproj" "$rid" "$output_dir/payloads/$rid/installer"
  publish_project "$root/LocalInstaller.Sample.Desktop/LocalInstaller.Sample.Desktop.csproj" "$rid" "$output_dir/payloads/$rid/sample-desktop"
  publish_project "$root/LocalInstaller.Sample.Server/LocalInstaller.Sample.Server.csproj" "$rid" "$output_dir/payloads/$rid/sample-server"
  publish_project "$root/LocalInstaller.Sample.Cli/LocalInstaller.Sample.Cli.csproj" "$rid" "$output_dir/payloads/$rid/sample-cli"
  publish_project "$root/LocalInstaller.Sample.Tray/LocalInstaller.Sample.Tray.csproj" "$rid" "$output_dir/payloads/$rid/sample-tray"
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
  "payloadLayout": "payloads/{rid}/{installer,sample-desktop,sample-server,sample-cli,sample-tray}",
  "toolLayout": "tools/{rid}/{packager,smoke}",
  "checksums": "checksums.sha256"
}
JSON
