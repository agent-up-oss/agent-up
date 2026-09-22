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

registry="${AGENTUP_CAPABILITY_REGISTRY_PATH:-$root/.agent-up-dev/capability-registry}"
./scripts/pack-first-party-capabilities.sh "$registry"
export AGENTUP_CAPABILITY_REGISTRY_PATH="$registry"

results="artifacts/test-results/runtime-capability-e2e"
mkdir -p "$results"

# Every wrapped launch enters the packed nix-shell, and on a cold runner the first one pays for
# the nixpkgs tarball and the whole SDK closure. The workspace starts its applications together,
# so the rest then queue on that one fetch rather than installing. Realise the two shells this
# test enables now, in the background, so the download overlaps the Release build that dotnet
# test does before its first assertion. A failure here is not fatal: the launch would do the
# same work itself, only later.
prewarm_log="$results/nix-prewarm.log"
prewarm_pid=""
if command -v nix-shell >/dev/null 2>&1; then
  (
    for module in dotnet docker; do
      for shell_nix in "$registry"/packages/"$module"/*/default.nix; do
        echo "prewarming $shell_nix"
        nix-shell "$shell_nix" --run true
      done
    done
  ) >"$prewarm_log" 2>&1 &
  prewarm_pid=$!
  echo "Warming the packed dotnet and docker shells in the background (log: $prewarm_log)."
fi

docker pull postgres:16

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

# This flow spends most of its time inside the fixture, before any assertion: starting the
# Server, mounting Desktop, enabling modules, then installing and launching five applications.
# At the default verbosity none of the fixture's progress reaches the job log, so a test host
# that dies takes the only account of how far it got with it - which is what "Test host process
# crashed" and nothing else looked like. The detailed logger prints that progress as it happens,
# and --blame-crash leaves a dump in the results directory the job already uploads.
test_cmd=(
  dotnet test "AgentUp.Tests/AgentUp.Tests.csproj"
  --configuration Release
  --results-directory "$results"
  --settings "coverlet.runsettings"
  --logger "trx;LogFileName=runtime-capability-e2e.trx"
  --logger "console;verbosity=detailed"
  --filter "Category=RuntimeCapabilityE2E"
  --blame-crash
  --blame-hang
  --blame-hang-timeout 25m
)
status=0
if command -v xvfb-run >/dev/null 2>&1 && command -v dbus-run-session >/dev/null 2>&1; then
  run_xvfb "${test_cmd[@]}" || status=$?
else
  # Ubuntu CI installs xvfb-run. Elsewhere the Linux fixture starts a private Xvfb.
  LIBGL_ALWAYS_SOFTWARE=1 \
    GALLIUM_DRIVER=llvmpipe \
    WEBKIT_DISABLE_COMPOSITING_MODE=1 \
    WEBKIT_DISABLE_DMABUF_RENDERER=1 \
    WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS=1 \
    "${test_cmd[@]}" || status=$?
fi

if [ -n "$prewarm_pid" ]; then
  wait "$prewarm_pid" || echo "The background shell warm-up exited non-zero." >&2
  tail -n 20 "$prewarm_log" || true
fi

exit "$status"
