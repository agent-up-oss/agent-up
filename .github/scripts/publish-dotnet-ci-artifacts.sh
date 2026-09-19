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

  dotnet restore "$root/agent-up.sln" \
    --runtime "$rid"
}

publish_project() {
  local project="$1"
  local rid="$2"
  local destination="$3"
  local sentry_dsn=""

  case "$project" in
    *AgentUp.Desktop*) sentry_dsn="${SENTRY_DSN_DESKTOP:-}" ;;
    *AgentUp.CLI*) sentry_dsn="${SENTRY_DSN_CLI:-}" ;;
  esac

  local extra=()
  if [ -n "$sentry_dsn" ]; then
    extra+=(-p:SentryDsn="$sentry_dsn")
  fi

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
    "${extra[@]}" \
    -o "$destination"
}

publish_test_runner() {
  local project="$1"
  local rid="$2"
  local destination="$3"

  # Keep the native E2E host as a plain directory: Avalonia and the platform WebView load
  # native/runtime assets by path, which is not reliable after single-file extraction on
  # hosted Windows runners.
  #
  # Framework-dependent, not self-contained. A self-contained publish of a test project
  # dropped System.Memory.dll from the runtime closure, so the host died before the first
  # test on every non-Linux RID: NUnitLite's option parser needs SearchValues<T> through
  # System.Text.RegularExpressions, and Avalonia's FontFamily cctor needs it too. The
  # platform runners install the matching .NET, so the shared framework supplies the BCL.
  dotnet publish "$project" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --no-restore \
    --self-contained false \
    -p:PublishSingleFile=false \
    -p:DebugType=none \
    -p:DebugSymbols=false \
    -p:Version="$version" \
    -o "$destination"
}

rm -rf "$output_dir"
mkdir -p "$output_dir/tools" "$output_dir/payloads"

for rid in "${rids[@]}"; do
  restore_runtime "$rid"
  publish_project "$root/AgentUp.Packaging/AgentUp.Packaging.csproj" "$rid" "$output_dir/tools/$rid/packaging"
  publish_project "$root/AgentUp.PackageSmoke/AgentUp.PackageSmoke.csproj" "$rid" "$output_dir/tools/$rid/package-smoke"
  publish_test_runner "$root/AgentUp.Tests/AgentUp.Tests.csproj" "$rid" "$output_dir/tools/$rid/agent-up-tests"
  publish_project "$root/AgentUp.InstallerApp/AgentUp.InstallerApp.csproj" "$rid" "$output_dir/payloads/$rid/installer"
  publish_project "$root/AgentUp.Desktop/AgentUp.Desktop.csproj" "$rid" "$output_dir/payloads/$rid/desktop"
  publish_project "$root/AgentUp.Server/AgentUp.Server.csproj" "$rid" "$output_dir/payloads/$rid/server"
  publish_project "$root/AgentUp.CLI/AgentUp.CLI.csproj" "$rid" "$output_dir/payloads/$rid/cli"
  publish_project "$root/AgentUp.Tray/AgentUp.Tray.csproj" "$rid" "$output_dir/payloads/$rid/tray"
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
  "payloadLayout": "payloads/{rid}/{installer,desktop,server,cli,tray}",
  "toolLayout": "tools/{rid}/{packaging,package-smoke,agent-up-tests}",
  "checksums": "checksums.sha256"
}
JSON
