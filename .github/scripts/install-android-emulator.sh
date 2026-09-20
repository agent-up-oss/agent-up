#!/usr/bin/env bash
# Installs the Android Emulator package before android-emulator-runner asks for it.
#
# That action always runs `sdkmanager --install emulator`, once, and treats a bad zip as a failed
# job. Google's emulator archive is occasionally not an archive:
#
#     Warning: An error occurred while preparing SDK package Android Emulator: Error on ZipFile unknown archive.
#
# The NDK step already retries the same class of truncated download. Doing it here means a bad
# zip costs a retry rather than an Android E2E run, and a half-written install is thrown away
# rather than left for the action to believe. When this succeeds, the action's own install is a
# no-op on the same package.
set -euo pipefail

sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
sdkmanager="$sdk/cmdline-tools/latest/bin/sdkmanager"
if [ -z "$sdk" ] || [ ! -x "$sdkmanager" ]; then
  echo "No sdkmanager at $sdkmanager; leaving the emulator package to android-emulator-runner."
  exit 0
fi

# Three attempts, because the failure this exists for is a truncated download rather than a wrong
# request: the same call a moment later is the whole fix.
for attempt in 1 2 3; do
  echo "Installing emulator (attempt $attempt)."
  if "$sdkmanager" --install emulator --channel=0; then
    echo "Installed emulator."
    exit 0
  fi
  # Whatever landed is not an emulator, and leaving it would let the later install find it and
  # trust it.
  rm -rf "$sdk/emulator"
  sleep $((attempt * 5))
done

echo "Could not install emulator after three attempts." >&2
exit 1
