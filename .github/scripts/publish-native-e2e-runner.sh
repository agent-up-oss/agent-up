#!/usr/bin/env bash
# Desktop · Native macOS and Windows: publishes the AgentUp.Tests NUnitLite runner for this
# runner's runtime, so the leg runs the native-display suite without waiting for Build.
#
#   ./.github/scripts/publish-native-e2e-runner.sh <rid> <output-dir>
set -euo pipefail

if [ "$#" -ne 2 ]; then
  echo "Usage: $0 <rid> <output-dir>" >&2
  exit 2
fi

rid="$1"
destination="$2"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
project="$root/AgentUp.Tests/AgentUp.Tests.csproj"

dotnet restore "$project" --runtime "$rid"

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
  --configuration Release \
  --runtime "$rid" \
  --no-restore \
  --self-contained false \
  -p:PublishSingleFile=false \
  -p:DebugType=none \
  -p:DebugSymbols=false \
  -o "$destination"
