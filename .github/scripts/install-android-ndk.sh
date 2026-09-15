#!/usr/bin/env bash
set -euo pipefail

android_dir="${1:-android}"
if [ ! -d "$android_dir" ]; then
  echo "Android project directory is missing: $android_dir" >&2
  exit 1
fi

if [ -z "${ANDROID_HOME:-}" ]; then
  echo "ANDROID_HOME is not set" >&2
  exit 1
fi

ndk_version="$(python3 - "$android_dir" <<'PY'
from pathlib import Path
import re
import sys

root = Path(sys.argv[1])
text = ""
for path in root.rglob("*.gradle*"):
    text += path.read_text()
match = re.search(r'ndkVersion\s*[=:]\s*"([^"]+)"', text)
print(match.group(1) if match else "")
PY
)"

if [ -n "$ndk_version" ]; then
  sdkmanager --install "ndk;${ndk_version}"
fi

printf 'sdk.dir=%s\n' "$ANDROID_HOME" > "${android_dir}/local.properties"
