#!/usr/bin/env bash
# Installs the NDK the Android build is going to ask for, before it asks, and writes sdk.dir for
# generated android/ trees that need a local.properties file after prebuild.
#
# Nothing preinstalls this version on the runner, so every build has had the Android Gradle Plugin
# fetch it mid-configuration - about a gigabyte, in a single attempt, with no retry and no check on
# what came back. On 1f92c40 what came back was not an archive:
#
#     Failed to install the following SDK components: ndk;27.1.12297006
#     Caused by: java.util.zip.ZipException: Archive is not a ZIP archive
#
# and it took a ten-minute build with it. Doing it here instead means a bad download costs a retry
# rather than a run, and a half-written install is thrown away rather than believed.
#
# The first argument is either an app root (AgentUp.Mobile.E2E.App) or a generated android/
# directory. Version comes from Gradle after prebuild when that tree exists, otherwise from Expo's
# plugin default - the same place Gradle would read it. If the version cannot be read, this still
# writes local.properties when it can, then leaves the NDK fetch to Gradle.
set -euo pipefail

target="${1:?Pass an app root or a generated android/ directory}"
if [ ! -d "$target" ]; then
  echo "Directory is missing: $target" >&2
  exit 1
fi

app=""
android_dir=""
if [ -d "$target/android" ]; then
  app="$target"
  android_dir="$target/android"
elif [ -f "$target/build.gradle" ] || [ -f "$target/build.gradle.kts" ] || [ -f "$target/settings.gradle" ] || [ -f "$target/settings.gradle.kts" ]; then
  android_dir="$target"
  app="$(dirname "$target")"
else
  app="$target"
fi

version=""
if [ -n "$android_dir" ] && [ -d "$android_dir" ]; then
  version="$(python3 - "$android_dir" <<'PY'
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
fi

if [ -z "$version" ] && [ -n "$app" ]; then
  plugin="$app/node_modules/expo-modules-autolinking/android/expo-gradle-plugin/expo-autolinking-plugin/src/main/kotlin/expo/modules/plugin/ExpoRootProjectPlugin.kt"
  if [ -f "$plugin" ]; then
    version="$(sed -n 's/.*getVersionOrDefault("ndkVersion", "\([^"]*\)").*/\1/p' "$plugin" | head -n 1)"
  fi
fi

sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
if [ -n "$android_dir" ] && [ -d "$android_dir" ]; then
  if [ -z "$sdk" ]; then
    echo "ANDROID_HOME is not set" >&2
    exit 1
  fi
  printf 'sdk.dir=%s\n' "$sdk" > "${android_dir}/local.properties"
fi

if [ -z "$version" ]; then
  echo "NDK version is unknown; leaving it to Gradle."
  exit 0
fi

sdkmanager=""
if [ -n "$sdk" ] && [ -x "$sdk/cmdline-tools/latest/bin/sdkmanager" ]; then
  sdkmanager="$sdk/cmdline-tools/latest/bin/sdkmanager"
elif command -v sdkmanager >/dev/null 2>&1; then
  sdkmanager="$(command -v sdkmanager)"
fi

if [ -z "$sdkmanager" ]; then
  echo "No sdkmanager found; leaving the NDK to Gradle."
  exit 0
fi

if [ -n "$sdk" ] && [ -d "$sdk/ndk/$version" ]; then
  echo "present: ndk;$version"
  exit 0
fi

# Three attempts, because the failure this exists for is a truncated download rather than a wrong
# request: the same call a moment later is the whole fix.
for attempt in 1 2 3; do
  echo "Installing ndk;$version (attempt $attempt)."
  if "$sdkmanager" --install "ndk;$version"; then
    echo "Installed ndk;$version."
    exit 0
  fi
  # Whatever landed is not an NDK, and leaving it would let the build find it and trust it.
  if [ -n "$sdk" ]; then
    rm -rf "$sdk/ndk/$version"
  fi
  sleep $((attempt * 5))
done

echo "Could not install ndk;$version after three attempts." >&2
exit 1
