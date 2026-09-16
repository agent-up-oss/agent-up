#!/usr/bin/env bash
set -euo pipefail

results_root="artifacts/test-kind-watchdog"
rm -rf "$results_root"
mkdir -p "$results_root"

if [[ -f /tmp/chromium-install.pid ]]; then
  chromium_pid="$(cat /tmp/chromium-install.pid)"
  echo "Waiting for background Chromium installation (PID $chromium_pid)"
  while kill -0 "$chromium_pid" 2>/dev/null; do sleep 2; done
  cat /tmp/chromium-install.log
fi
export PUPPETEER_EXECUTABLE_PATH="${PUPPETEER_EXECUTABLE_PATH:-$(command -v google-chrome-stable 2>/dev/null || command -v google-chrome 2>/dev/null || command -v chromium 2>/dev/null || command -v chromium-browser 2>/dev/null || true)}"

run_tier() {
  local tier="$1" budget="$2" filter="$3" display="$4"; shift 4
  echo "Running $tier tier with ${budget}s wall-clock budget"
  local start=$SECONDS
  local status=0
  timeout --signal=TERM --kill-after=10s "${budget}s" bash -c '
    set -euo pipefail
    tier="$1"; filter="$2"; display="$3"; results_root="$4"; shift 4
    for project in "$@"; do
      result="$results_root/$tier-$project.trx"
      command=(dotnet test "$project" --configuration Release --no-build --no-restore --settings .github/test-kind.runsettings --filter "$filter" --logger "console;verbosity=minimal" --logger "trx;LogFileName=$PWD/$result")
      if [[ "$display" == "xvfb" ]]; then
        command=(env LIBGL_ALWAYS_SOFTWARE=1 GALLIUM_DRIVER=llvmpipe WEBKIT_DISABLE_COMPOSITING_MODE=1 WEBKIT_DISABLE_DMABUF_RENDERER=1 WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS=1 dbus-run-session xvfb-run -a --server-args="-screen 0 1280x720x24 -ac +extension GLX +render -noreset" "${command[@]}")
      fi
      "${command[@]}"
      python3 .github/scripts/assert-trx-has-tests.py "$result"
    done
  ' _ "$tier" "$filter" "$display" "$results_root" "$@" || status=$?
  if [[ $status -ne 0 ]]; then
    if [[ $status -eq 124 || $status -eq 137 ]]; then
      echo "$tier tier exceeded its ${budget}s wall-clock budget" >&2
    fi
    return "$status"
  fi
  echo "$tier tier completed in $((SECONDS - start))s"
}

run_tier unit 60 'FullyQualifiedName~.Unit.' none AgentUp.Server.Tests AgentUp.Desktop.Tests
run_tier provider 75 'FullyQualifiedName~.Provider.' xvfb AgentUp.Server.Tests AgentUp.Desktop.Tests
run_tier e2e 180 'FullyQualifiedName~.E2E.' xvfb AgentUp.Tests
