#!/usr/bin/env bash
set -euo pipefail

# Starts the repository-root agent-up.json through Desktop, then replays the
# recorded Example Web validation flow. That is the runtime-capability contract:
# docker hosts Postgres, dotnet hosts the API, and the connected web app shows
# live data in the same workspace as Docs and Sample Desktop.

root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$root"

if ! command -v docker >/dev/null 2>&1; then
  echo "docker is required for runtime-capability-e2e" >&2
  exit 1
fi

# Agent CLIs are capability packages. Do not let shell.nix download Cursor/Codex/Claude
# as a side effect of the Linux fixture importing native libraries.
export AGENTUP_SKIP_DEV_AGENT_BOOTSTRAP=1

docker pull postgres:16

registry="${AGENTUP_CAPABILITY_REGISTRY_PATH:-$root/.agent-up-dev/capability-registry}"
./scripts/pack-first-party-capabilities.sh "$registry"
export AGENTUP_CAPABILITY_REGISTRY_PATH="$registry"

run_xvfb() {
  LIBGL_ALWAYS_SOFTWARE=1 \
    GALLIUM_DRIVER=llvmpipe \
    WEBKIT_DISABLE_COMPOSITING_MODE=1 \
    WEBKIT_DISABLE_DMABUF_RENDERER=1 \
    WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS=1 \
    dbus-run-session xvfb-run -a \
    --server-args="-screen 0 1280x720x24 -ac +extension GLX +render -noreset" \
    "$@"
}

mkdir -p artifacts/test-results/runtime-capability-e2e
# This flow spends most of its time inside the fixture, before any assertion: starting the
# Server, mounting Desktop, enabling modules, then installing and launching five applications.
# At the default verbosity none of the fixture's progress reaches the job log, so a test host
# that dies takes the only account of how far it got with it - which is what "Test host process
# crashed" and nothing else looked like. The detailed logger prints that progress as it happens,
# and --blame-crash leaves a dump in the results directory the job already uploads.
test_cmd=(
  dotnet test "AgentUp.Tests/AgentUp.Tests.csproj"
  --configuration Release
  --results-directory "artifacts/test-results/runtime-capability-e2e"
  --settings "coverlet.runsettings"
  --logger "trx;LogFileName=runtime-capability-e2e.trx"
  --logger "console;verbosity=detailed"
  --filter "Category=RuntimeCapabilityE2E"
  --blame-crash
  --blame-hang
  --blame-hang-timeout 25m
)
if command -v xvfb-run >/dev/null 2>&1 && command -v dbus-run-session >/dev/null 2>&1; then
  run_xvfb "${test_cmd[@]}"
else
  # Ubuntu CI installs xvfb-run. Elsewhere the Linux fixture starts a private Xvfb.
  LIBGL_ALWAYS_SOFTWARE=1 \
    GALLIUM_DRIVER=llvmpipe \
    WEBKIT_DISABLE_COMPOSITING_MODE=1 \
    WEBKIT_DISABLE_DMABUF_RENDERER=1 \
    WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS=1 \
    "${test_cmd[@]}"
fi
