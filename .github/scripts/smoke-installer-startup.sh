#!/usr/bin/env bash
set -euo pipefail

platform="$1"
payload_root="${2:-}"
installer="${AGENTUP_INSTALLER_APP_COMMAND:-}"

if [ -z "$installer" ]; then
  echo "AGENTUP_INSTALLER_APP_COMMAND not set, skipping startup smoke" >&2
  exit 0
fi

if [ "$platform" != "macos" ]; then
  echo "Startup smoke not applicable for $platform, skipping"
  exit 0
fi

LOG="$HOME/Library/Logs/Agent-Up/installer.log"
PROCESS_LOG="$HOME/Library/Logs/Agent-Up/installer-startup-process.log"
rm -f "$LOG"
rm -f "$PROCESS_LOG"

if [ -n "$payload_root" ]; then
  "$installer" --payload-root "$payload_root" >"$PROCESS_LOG" 2>&1 &
else
  "$installer" >"$PROCESS_LOG" 2>&1 &
fi
INSTALLER_PID=$!

TIMEOUT_SECS="${AGENTUP_INSTALLER_STARTUP_TIMEOUT_SECS:-60}"
DEADLINE=$((SECONDS + TIMEOUT_SECS))
SUCCESS=false
PROCESS_EXITED=false
PROCESS_STATUS=0
while [ "$SECONDS" -lt "$DEADLINE" ]; do
  if [ -f "$LOG" ] && grep -q "Creating installer window" "$LOG"; then
    SUCCESS=true
    break
  fi

  if ! kill -0 "$INSTALLER_PID" 2>/dev/null; then
    PROCESS_EXITED=true
    set +e
    wait "$INSTALLER_PID"
    PROCESS_STATUS=$?
    set -e
    break
  fi

  sleep 1
done

if [ "$PROCESS_EXITED" != "true" ]; then
  kill "$INSTALLER_PID" 2>/dev/null || true
  wait "$INSTALLER_PID" 2>/dev/null || true
fi

echo "=== Installer log ==="
cat "$LOG" 2>/dev/null || echo "(no log file written)"
echo "====================="
echo "=== Installer process output ==="
cat "$PROCESS_LOG" 2>/dev/null || echo "(no process output written)"
echo "================================"

if [ "$SUCCESS" != "true" ]; then
  if [ "$PROCESS_EXITED" = "true" ]; then
    echo "Installer process exited before window creation with status $PROCESS_STATUS" >&2
  fi
  echo "FAIL: Installer did not reach window creation within ${TIMEOUT_SECS}s" >&2
  exit 1
fi

echo "Installer startup smoke passed"
