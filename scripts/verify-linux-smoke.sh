#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${AGENTUP_SMOKE_VERSION:-0.0.0-local}"
configuration="${CONFIGURATION:-Release}"
rid="linux-x64"
payload_root="$root/artifacts/linux-smoke/payloads/$rid"
artifact_dir="$root/artifacts/linux-smoke/release-artifacts"

publish_payload() {
  local project="$1"
  local destination="$2"

  dotnet publish "$root/$project/$project.csproj" \
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

cd "$root"
rm -rf "$root/artifacts/linux-smoke"
mkdir -p "$payload_root" "$artifact_dir"

publish_payload AgentUp.InstallerApp "$payload_root/installer"
publish_payload AgentUp.Desktop "$payload_root/desktop"
publish_payload AgentUp.Server "$payload_root/server"
publish_payload AgentUp.CLI "$payload_root/cli"
publish_payload AgentUp.Tray "$payload_root/tray"

./scripts/package-release.sh ubuntu "$rid" "$version" "$artifact_dir" \
  --payload-root "$payload_root"
./.github/scripts/smoke-package.sh ubuntu "$rid" "$artifact_dir"
