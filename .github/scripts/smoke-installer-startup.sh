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
rm -f "$LOG"

if [ -n "$payload_root" ]; then
  "$installer" --payload-root "$payload_root" &
else
  "$installer" &
fi
INSTALLER_PID=$!

TIMEOUT_SECS=15
ELAPSED=0
SUCCESS=false
while [ "$ELAPSED" -lt "$TIMEOUT_SECS" ]; do
  if [ -f "$LOG" ] && grep -q "Creating installer window" "$LOG"; then
    SUCCESS=true
    break
  fi
  sleep 0.5
  ELAPSED=$((ELAPSED + 1))
done

kill "$INSTALLER_PID" 2>/dev/null || true
wait "$INSTALLER_PID" 2>/dev/null || true

echo "=== Installer log ==="
cat "$LOG" 2>/dev/null || echo "(no log file written)"
echo "====================="

if [ "$SUCCESS" != "true" ]; then
  echo "FAIL: Installer did not reach window creation within ${TIMEOUT_SECS}s" >&2
  exit 1
fi

echo "Installer startup smoke passed"
