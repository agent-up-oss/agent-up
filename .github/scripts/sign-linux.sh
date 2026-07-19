#!/usr/bin/env bash
# GPG-signs the .deb package and produces a detached .asc signature.
# Runs AFTER packaging.
set -euo pipefail

ARTIFACTS_DIR="${1:?Usage: sign-linux.sh <artifacts-dir>}"

if [ -z "${GPG_SIGNING_PRIVATE_KEY:-}" ]; then
  echo "::notice::Linux package signing skipped — GPG_SIGNING_PRIVATE_KEY not set"
  exit 0
fi

DEB_FILE=$(find "$ARTIFACTS_DIR" -maxdepth 1 -name "*.deb" | head -1)
if [ -z "$DEB_FILE" ]; then
  echo "::error::No .deb file found in $ARTIFACTS_DIR"
  exit 1
fi

# Import the private key
GPG_IMPORT_OUTPUT=$(echo "$GPG_SIGNING_PRIVATE_KEY" | \
  gpg --batch --yes --passphrase "${GPG_SIGNING_PASSPHRASE}" \
      --pinentry-mode loopback --import 2>&1)
echo "$GPG_IMPORT_OUTPUT"

GPG_KEY_ID=$(echo "$GPG_IMPORT_OUTPUT" | grep -oP '(?<=key )[0-9A-F]+(?=:)' | head -1)
if [ -z "$GPG_KEY_ID" ]; then
  GPG_KEY_ID=$(gpg --list-secret-keys --keyid-format LONG 2>/dev/null | grep '^sec' | head -1 | awk '{print $2}' | cut -d'/' -f2)
fi

if [ -z "$GPG_KEY_ID" ]; then
  echo "::error::Could not determine GPG key ID after import"
  exit 1
fi
echo "Signing with GPG key: $GPG_KEY_ID"

gpg --batch --yes \
    --passphrase "${GPG_SIGNING_PASSPHRASE}" \
    --pinentry-mode loopback \
    --detach-sign --armor \
    --local-user "$GPG_KEY_ID" \
    "$DEB_FILE"

echo "Signed: $DEB_FILE"
echo "Signature: ${DEB_FILE}.asc"
echo "Verify with: gpg --verify ${DEB_FILE##*/}.asc ${DEB_FILE##*/}"
