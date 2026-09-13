#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -lt 2 ]; then
  echo "Usage: $0 <artifact> <aab|ipa> [bundletool-jar]" >&2
  exit 2
fi

artifact="$1"
kind="$2"
bundletool_jar="${3:-}"
checksum_file="${artifact}.sha256"
minimum_bytes=1048576

check_file() {
  local file="$1"
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum -c "$file"
  else
    shasum -a 256 -c "$file"
  fi
}

if [ ! -s "$artifact" ]; then
  echo "Missing mobile artifact: $artifact" >&2
  exit 1
fi

if [ ! -s "$checksum_file" ]; then
  echo "Missing checksum file: $checksum_file" >&2
  exit 1
fi

actual_size="$(wc -c < "$artifact" | tr -d ' ')"
if [ "$actual_size" -lt "$minimum_bytes" ]; then
  echo "Artifact is too small to be a store binary: $artifact ($actual_size bytes)" >&2
  exit 1
fi

(
  cd "$(dirname "$artifact")"
  check_file "$(basename "$checksum_file")"
)

case "$kind" in
  aab)
    if [ -z "$bundletool_jar" ] || [ ! -s "$bundletool_jar" ]; then
      echo "bundletool jar is required to validate an AAB" >&2
      exit 1
    fi
    java -jar "$bundletool_jar" validate --bundle="$artifact"
    ;;
  ipa)
    if ! unzip -l "$artifact" | grep -Eq 'Payload/[^/]+\.app/'; then
      echo "IPA does not contain Payload/*.app: $artifact" >&2
      unzip -l "$artifact" >&2
      exit 1
    fi
    ;;
  *)
    echo "Unknown artifact kind: $kind" >&2
    exit 2
    ;;
esac
