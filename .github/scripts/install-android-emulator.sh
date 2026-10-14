#!/usr/bin/env bash
# Installs the packages android-emulator-runner asks for, before it asks, with retries.
#
# That action always runs `sdkmanager --install` once per package and treats a bad zip as a
# failed job. Google's archives are occasionally not archives:
#
#     Warning: An error occurred while preparing SDK package Android Emulator: Error on ZipFile unknown archive.
#     Warning: An error occurred while preparing SDK package Google APIs Intel x86_64 Atom System Image: Error on ZipFile unknown archive.
#
# The NDK step already retries the same class of truncated download. Doing it here means a bad
# zip costs a retry rather than an Android E2E run, and a half-written install is thrown away
# rather than left for the action to believe. When this succeeds, the action's own install is a
# no-op on the same packages.
#
# The system-image id must match the android-emulator-runner inputs in
# .github/workflows/mobile-agent-auth-ci.yml (api-level 35, google_apis, x86_64).
set -euo pipefail

SYSTEM_IMAGE='system-images;android-35;google_apis;x86_64'
SYSTEM_IMAGE_DIR='system-images/android-35/google_apis/x86_64'
RETRY_SLEEP="${AGENTUP_SDK_RETRY_SLEEP:-5}"

sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
sdkmanager="$sdk/cmdline-tools/latest/bin/sdkmanager"
if [ -z "$sdk" ] || [ ! -x "$sdkmanager" ]; then
  echo "No sdkmanager at $sdkmanager; leaving emulator packages to android-emulator-runner."
  exit 0
fi

# True when sdkmanager reported a truncated zip. It has printed this warning and still exited 0.
install_output_failed() {
  grep -q 'unknown archive' <<<"$1"
}

install_package() {
  local package="$1"
  local installed="$2"
  local attempt status output
  for attempt in 1 2 3; do
    echo "Installing ${package} (attempt ${attempt})."
    status=0
    output="$("$sdkmanager" --install "$package" --channel=0 2>&1)" || status=$?
    printf '%s\n' "$output"
    if [ "$status" -eq 0 ] && [ -e "$installed" ] && ! install_output_failed "$output"; then
      echo "Installed ${package}."
      return 0
    fi
    # Whatever landed is not the package, and leaving it would let the later install find it and
    # trust it.
    rm -rf "$installed"
    sleep $((attempt * RETRY_SLEEP))
  done
  echo "Could not install ${package} after three attempts." >&2
  return 1
}

install_package emulator "$sdk/emulator"
install_package "$SYSTEM_IMAGE" "$sdk/$SYSTEM_IMAGE_DIR"
