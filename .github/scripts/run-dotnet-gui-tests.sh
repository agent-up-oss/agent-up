#!/usr/bin/env bash
set -uo pipefail

# Runs AgentUp.Tests: native-display (non-E2E) first, then HeadlessE2E once Chromium
# is available. Continues after a failing attempt so both TRX logs still exist.

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

failed=0

test_status=1
for attempt in 1 2; do
  echo "Running AgentUp.Tests (non-E2E) on Ubuntu attempt $attempt/2"
  if run_xvfb dotnet test "AgentUp.Tests/AgentUp.Tests.csproj" \
    --configuration Release \
    --results-directory "artifacts/test-results/AgentUp.Tests" \
    --settings "coverlet.runsettings" \
    --logger "trx;LogFileName=test-results.trx" \
    --collect:"XPlat Code Coverage" \
    --blame-hang \
    --blame-hang-timeout 120s \
    --filter "Category!=HeadlessE2E"; then
    test_status=0
    break
  else
    test_status=$?
    echo "=== WebKit/GTK subprocesses still running after attempt $attempt ==="
    pgrep -la -f "WebKit|webkit|WebProcess|NetworkProcess" 2>/dev/null || echo "(none)"
    echo "=== dmesg tail (crash context) ==="
    dmesg 2>/dev/null | tail -20 || true
    echo "=== Killing lingering WebKit subprocesses before retry ==="
    pkill -9 -f "WebKitWebProcess|WebKitNetworkProcess|webkit_webproc" 2>/dev/null || true
    sleep 2
  fi
done
if [ "$test_status" -ne 0 ]; then
  failed=1
fi

if [ -f /tmp/chromium-install.pid ]; then
  chromium_pid="$(cat /tmp/chromium-install.pid)"
  echo "Waiting for background Chromium installation (PID $chromium_pid)…"
  while kill -0 "$chromium_pid" 2>/dev/null; do sleep 2; done
  echo "Chromium installation finished. Log:"
  cat /tmp/chromium-install.log || true
  chromium_exe="$(command -v chromium 2>/dev/null || command -v chromium-browser 2>/dev/null || true)"
  if [ -n "$chromium_exe" ]; then
    export PUPPETEER_EXECUTABLE_PATH="$chromium_exe"
  fi
fi

chromium_exe="${PUPPETEER_EXECUTABLE_PATH:-}"
if [ -z "$chromium_exe" ]; then
  echo "No Chromium binary found — HeadlessE2E tests cannot run"
  failed=1
else
  echo "Running HeadlessE2E tests with $chromium_exe"
  if ! PUPPETEER_EXECUTABLE_PATH="$chromium_exe" \
    LIBGL_ALWAYS_SOFTWARE=1 \
    GALLIUM_DRIVER=llvmpipe \
    WEBKIT_DISABLE_DMABUF_RENDERER=1 \
    WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS=1 \
    dbus-run-session xvfb-run -a \
    --server-args="-screen 0 1280x720x24 -ac +extension GLX +render -noreset" \
    dotnet test "AgentUp.Tests/AgentUp.Tests.csproj" \
      --configuration Release \
      --results-directory "artifacts/test-results/AgentUp.Tests" \
      --settings "coverlet.runsettings" \
      --logger "trx;LogFileName=e2e-results.trx" \
      --collect:"XPlat Code Coverage" \
      --filter "Category=HeadlessE2E" \
      --blame-hang \
      --blame-hang-timeout 300s; then
    failed=1
  fi
fi

exit "$failed"
