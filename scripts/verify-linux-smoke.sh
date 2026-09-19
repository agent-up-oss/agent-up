#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${AGENTUP_SMOKE_VERSION:-0.0.0-local}"
configuration="${CONFIGURATION:-Release}"
rid="linux-x64"
payload_root="$root/artifacts/linux-smoke/payloads/$rid"
tool_root="$root/artifacts/linux-smoke/tools/$rid"
artifact_dir="artifacts/linux-smoke/release-artifacts"

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
mkdir -p "$payload_root" "$root/$artifact_dir"

publish_payload AgentUp.InstallerApp "$payload_root/installer"
publish_payload AgentUp.Desktop "$payload_root/desktop"
publish_payload AgentUp.Server "$payload_root/server"
publish_payload AgentUp.CLI "$payload_root/cli"
publish_payload AgentUp.Tray "$payload_root/tray"
publish_payload AgentUp.Packaging "$tool_root/packaging"
publish_payload AgentUp.PackageSmoke "$tool_root/package-smoke"

export AGENTUP_PACKAGING_COMMAND="$tool_root/packaging/AgentUp.Packaging"
./scripts/package-ubuntu.sh "$rid" "$version" "$artifact_dir" \
  --payload-root "$payload_root"
export AGENTUP_PACKAGE_SMOKE_COMMAND="$tool_root/package-smoke/AgentUp.PackageSmoke"
./.github/scripts/smoke-package.sh ubuntu "$rid" "$artifact_dir"
