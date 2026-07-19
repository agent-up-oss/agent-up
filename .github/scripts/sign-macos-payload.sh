#!/usr/bin/env bash
# Signs all Mach-O executables in the payload directory with Developer ID Application.
# Must run BEFORE packaging so signed binaries are baked into the .pkg.
set -euo pipefail

RID="${1:?Usage: sign-macos-payload.sh <rid> <payload-dir>}"
PAYLOAD_DIR="${2:?Usage: sign-macos-payload.sh <rid> <payload-dir>}"

if [ -z "${MACOS_APP_CERTIFICATE:-}" ]; then
  echo "::notice::macOS payload signing skipped — MACOS_APP_CERTIFICATE not set"
  exit 0
fi

KEYCHAIN_PATH="$RUNNER_TEMP/macos-app-signing.keychain-db"
CERT_PATH="$RUNNER_TEMP/macos-app-cert.p12"

cleanup() {
  security delete-keychain "$KEYCHAIN_PATH" 2>/dev/null || true
  rm -f "$CERT_PATH"
}
trap cleanup EXIT

security create-keychain -p "${KEYCHAIN_PASSWORD}" "$KEYCHAIN_PATH"
security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
security unlock-keychain -p "${KEYCHAIN_PASSWORD}" "$KEYCHAIN_PATH"
security list-keychains -d user -s "$KEYCHAIN_PATH" $(security list-keychains -d user | sed s/\"//g)

echo "$MACOS_APP_CERTIFICATE" | base64 --decode > "$CERT_PATH"
security import "$CERT_PATH" -k "$KEYCHAIN_PATH" -P "${MACOS_APP_CERTIFICATE_PASSWORD}" -T /usr/bin/codesign
security set-key-partition-list -S apple-tool:,apple: -s -k "${KEYCHAIN_PASSWORD}" "$KEYCHAIN_PATH"

IDENTITY=$(security find-identity -v -p codesigning "$KEYCHAIN_PATH" | grep "Developer ID Application" | head -1 | awk '{print $2}')
if [ -z "$IDENTITY" ]; then
  echo "::error::No 'Developer ID Application' identity found after certificate import"
  exit 1
fi
echo "Signing identity: $IDENTITY"

signed_count=0
while IFS= read -r -d "" binary; do
  if file "$binary" | grep -qE "Mach-O (64-bit )?executable|Mach-O universal binary"; then
    codesign --force --verify --verbose=1 \
      --sign "$IDENTITY" \
      --options runtime \
      --timestamp \
      --entitlements "packaging/macos/entitlements.plist" \
      "$binary"
    signed_count=$((signed_count + 1))
  fi
done < <(find "$PAYLOAD_DIR" -type f -print0)

echo "Signed $signed_count Mach-O executable(s) in $PAYLOAD_DIR"
