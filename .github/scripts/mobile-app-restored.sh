#!/usr/bin/env bash
# Whether a restored build is actually usable.
#
# A cache hit is not the same as having the app. A restore that brought back only some of the
# binaries leaves the runner reporting a missing one, which reads as a broken suite rather than
# as a cache that needs rebuilding - and the build it skipped is the very thing that would have
# fixed it. So every binary the platform needs is checked by name, and anything missing means
# build it after all.
set -euo pipefail

platform="${1:?Pass the platform to check: android or ios}"

case "$platform" in
  android)
    # Detox installs both: the app, and the instrumentation that drives it.
    required=(
      "AgentUp.Mobile/android/app/build/outputs/apk/release/app-release.apk"
      "AgentUp.Mobile/android/app/build/outputs/apk/androidTest/release/app-release-androidTest.apk"
    )
    ;;
  ios)
    # The bundle is a directory, so the executable inside it is what proves it arrived whole.
    required=(
      "AgentUp.Mobile/ios/build/Build/Products/Release-iphonesimulator/AgentUp.app/AgentUp"
    )
    ;;
  *)
    echo "Unknown platform '$platform'. Use android or ios." >&2
    exit 2
    ;;
esac

missing=0
for path in "${required[@]}"; do
  if [ -e "$path" ]; then
    echo "present: $path"
  else
    echo "MISSING: $path"
    missing=1
  fi
done

if [ "$missing" -eq 0 ]; then
  echo "The restored $platform app is complete; the build is skipped."
  echo "usable=true" >> "${GITHUB_OUTPUT:-/dev/stdout}"
else
  echo "The restored $platform app is incomplete; building it."
  echo "usable=false" >> "${GITHUB_OUTPUT:-/dev/stdout}"
fi
