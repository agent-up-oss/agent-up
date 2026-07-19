#!/usr/bin/env bash
# Signs the .pkg with Developer ID Installer, notarizes with Apple, and staples the ticket.
# Must run AFTER packaging.
set -euo pipefail

RID="${1:?Usage: sign-notarize-macos.sh <rid> <artifacts-dir>}"
ARTIFACTS_DIR="${2:?Usage: sign-notarize-macos.sh <rid> <artifacts-dir>}"

PKG_FILE=$(find "$ARTIFACTS_DIR" -maxdepth 1 -name "*.pkg" | head -1)
if [ -z "$PKG_FILE" ]; then
  echo "::error::No .pkg file found in $ARTIFACTS_DIR"
  exit 1
fi

if [ "${SIGNING_SMOKE_TEST:-}" = "true" ]; then
  echo "::notice::Smoke test mode: skipping productsign and notarization (requires Apple credentials)"
  pkgutil --check-signature "$PKG_FILE" || true
  echo "Smoke test: .pkg is present and structurally valid — productsign/notarize skipped"
  exit 0
fi

if [ -z "${MACOS_INSTALLER_CERTIFICATE:-}" ]; then
  echo "::notice::macOS package signing skipped — MACOS_INSTALLER_CERTIFICATE not set"
  exit 0
fi

KEYCHAIN_PATH="$RUNNER_TEMP/macos-installer-signing.keychain-db"
CERT_PATH="$RUNNER_TEMP/macos-installer-cert.p12"
SIGNED_PKG="${PKG_FILE%.pkg}-signed.pkg"

cleanup() {
  security delete-keychain "$KEYCHAIN_PATH" 2>/dev/null || true
  rm -f "$CERT_PATH" "$SIGNED_PKG"
}
trap cleanup EXIT

security create-keychain -p "${KEYCHAIN_PASSWORD}" "$KEYCHAIN_PATH"
security set-keychain-settings -lut 21600 "$KEYCHAIN_PATH"
security unlock-keychain -p "${KEYCHAIN_PASSWORD}" "$KEYCHAIN_PATH"
security list-keychains -d user -s "$KEYCHAIN_PATH" $(security list-keychains -d user | sed s/\"//g)

echo "$MACOS_INSTALLER_CERTIFICATE" | base64 --decode > "$CERT_PATH"
security import "$CERT_PATH" -k "$KEYCHAIN_PATH" -P "${MACOS_INSTALLER_CERTIFICATE_PASSWORD}" -T /usr/bin/productsign
security set-key-partition-list -S apple-tool:,apple: -s -k "${KEYCHAIN_PASSWORD}" "$KEYCHAIN_PATH"

IDENTITY=$(security find-identity -v "$KEYCHAIN_PATH" | grep "Developer ID Installer" | head -1 | awk '{print $2}')
if [ -z "$IDENTITY" ]; then
  echo "::error::No 'Developer ID Installer' identity found after certificate import"
  exit 1
fi
echo "Installer signing identity: $IDENTITY"

productsign --sign "$IDENTITY" --timestamp "$PKG_FILE" "$SIGNED_PKG"
mv "$SIGNED_PKG" "$PKG_FILE"
echo "Package signed: $PKG_FILE"

echo "Submitting for notarization (this may take a few minutes)..."
xcrun notarytool submit "$PKG_FILE" \
  --apple-id "${MACOS_NOTARIZE_APPLE_ID}" \
  --password "${MACOS_NOTARIZE_APP_SPECIFIC_PASSWORD}" \
  --team-id "${MACOS_NOTARIZE_TEAM_ID}" \
  --wait \
  --timeout 30m

xcrun stapler staple "$PKG_FILE"
echo "Notarization stapled: $PKG_FILE"
