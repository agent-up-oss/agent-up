#!/usr/bin/env bash
# Installs the NDK the Android build is going to ask for, before it asks.
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
# The version is read from Expo's own plugin rather than written down again: that file is where
# Gradle gets it, so an upgrade that moves it moves this too. If it cannot be read, this does
# nothing and leaves the build to fetch it exactly as it does today - a silent no-op is the right
# failure for a step that only exists to make a later one more reliable.
set -euo pipefail

app="${1:?Pass the app whose Android build this is for, e.g. AgentUp.Mobile.E2E.App}"

plugin="$app/node_modules/expo-modules-autolinking/android/expo-gradle-plugin/expo-autolinking-plugin/src/main/kotlin/expo/modules/plugin/ExpoRootProjectPlugin.kt"
if [ ! -f "$plugin" ]; then
  echo "No Expo root project plugin at $plugin, so the NDK version is unknown; leaving it to Gradle."
  exit 0
fi

version="$(sed -n 's/.*getVersionOrDefault("ndkVersion", "\([^"]*\)").*/\1/p' "$plugin" | head -n 1)"
if [ -z "$version" ]; then
  echo "$plugin no longer states a default ndkVersion; leaving it to Gradle."
  exit 0
fi

sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
sdkmanager="$sdk/cmdline-tools/latest/bin/sdkmanager"
if [ -z "$sdk" ] || [ ! -x "$sdkmanager" ]; then
  echo "No sdkmanager at $sdkmanager; leaving the NDK to Gradle."
  exit 0
fi

if [ -d "$sdk/ndk/$version" ]; then
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
  rm -rf "$sdk/ndk/$version"
  sleep $((attempt * 5))
done

echo "Could not install ndk;$version after three attempts." >&2
exit 1
