#!/usr/bin/env bash
set -euo pipefail

platform="$1"
payload_root="${2:-}"
installer="${AGENTUP_INSTALLER_APP_COMMAND:-}"

if [ -z "$installer" ]; then
  echo "AGENTUP_INSTALLER_APP_COMMAND not set, skipping elevation smoke" >&2
  exit 0
fi

if [ "$platform" != "macos" ]; then
  echo "Elevation smoke not applicable for $platform, skipping"
  exit 0
fi

MOCK_BIN="$(mktemp -d)"
OSASCRIPT_LOG="$MOCK_BIN/osascript-calls.log"

# Stub osascript: capture the invocation and return success without running the script.
# We intentionally do not execute the script — this test only verifies that elevation
# was requested with the right mechanism (do shell script ... with administrator privileges).
cat > "$MOCK_BIN/osascript" << 'STUB_EOF'
#!/usr/bin/env bash
echo "$*" >> "$OSASCRIPT_ARGS_LOG"
STUB_EOF
chmod +x "$MOCK_BIN/osascript"

OSASCRIPT_ARGS_LOG="$OSASCRIPT_LOG" PATH="$MOCK_BIN:$PATH" \
  "$installer" --install-core ${payload_root:+--payload-root "$payload_root"} || true

echo "=== osascript invocations ==="
cat "$OSASCRIPT_LOG" 2>/dev/null || echo "(osascript was never called)"
echo "============================="

if ! [ -f "$OSASCRIPT_LOG" ]; then
  echo "FAIL: osascript was never invoked — installer did not request elevation" >&2
  rm -rf "$MOCK_BIN"
  exit 1
fi

if ! grep -q "administrator privileges" "$OSASCRIPT_LOG"; then
  echo "FAIL: osascript call did not include 'administrator privileges'" >&2
  rm -rf "$MOCK_BIN"
  exit 1
fi

rm -rf "$MOCK_BIN"
echo "Installer elevation smoke passed"
