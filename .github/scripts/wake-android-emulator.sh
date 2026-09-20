#!/usr/bin/env bash
# Gets the emulator to the point where a test can actually touch it.
#
# The emulator cold boots on every run - it is started with -no-snapshot-save, and its own log says
# so: "there's no snapshot and you have configured it not to save on exit". Boot completing is not
# the same as being usable: for a while afterwards the lock screen still holds the window, and
# Espresso refuses to interact with a root that has no window focus:
#
#     Waited for the root of the view hierarchy to have window focus and not request layout for 10
#     seconds. Root{... has-window-focus=false ...}
#
# The app is up and drawn when that happens; it simply is not the window the system is listening
# to. So this waits for the window manager to name a focused window, waking and unlocking on each
# attempt, and only then lets the suite start.
#
# A focused window is still not enough. Hosted google_apis images often leave
# com.google.android.googlesdksetup in an Application Not Responding dialog, and that dialog holds
# focus. Treating it as ready is how a run reported "awake and focused" and then failed every
# Detox scenario. The first-run wizard is skipped and those packages are stopped until a normal
# window holds focus.
#
# A condition rather than a sleep, deliberately: a fixed delay long enough for a slow runner is
# waste on a fast one, and short enough for a fast one is a test that fails on a slow one.
set -euo pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
focus_helper="$script_dir/android-emulator-focus.mjs"

adb="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}/platform-tools/adb"
if [ ! -x "$adb" ]; then
  adb="$(command -v adb || true)"
fi
if [ -z "$adb" ]; then
  echo "No adb on this machine, so there is no emulator to wake; leaving it to the suite." >&2
  exit 0
fi

"$adb" wait-for-device

# A focused window now does not guarantee a focused window after the harness has spent a minute
# starting the Server and test agents. Hosted emulators use the normal Android screen timeout, so
# the display can lock between this check and Detox's first lookup. Espresso then sees the app's
# still-visible root without window focus and waits until the test fails. Keep the CI device awake
# while it is powered and disable its screen timeout before making the one-time focus check.
"$adb" shell svc power stayon true
"$adb" shell settings put system screen_off_timeout 2147483647

# Skip the first-run wizard so it cannot ANR over the suite. Best-effort: a device that is already
# provisioned ignores these, and a package that is not installed is not worth failing the run over.
"$adb" shell settings put global device_provisioned 1 >/dev/null 2>&1 || true
"$adb" shell settings put secure user_setup_complete 1 >/dev/null 2>&1 || true
"$adb" shell pm disable-user --user 0 com.google.android.googlesdksetup >/dev/null 2>&1 || true
"$adb" shell pm disable-user --user 0 com.google.android.setupwizard >/dev/null 2>&1 || true
"$adb" shell am force-stop com.google.android.googlesdksetup >/dev/null 2>&1 || true
"$adb" shell am force-stop com.google.android.setupwizard >/dev/null 2>&1 || true

attempt=1
while [ "$attempt" -le 60 ]; do
  # Both are best-effort: on a device that is already awake and unlocked they change nothing, and
  # neither is worth failing the run over.
  "$adb" shell input keyevent KEYCODE_WAKEUP >/dev/null 2>&1 || true
  "$adb" shell wm dismiss-keyguard >/dev/null 2>&1 || true

  # mCurrentFocus is null while nothing holds focus, which is the state Espresso gives up on.
  # An Application Not Responding dialog also reports a focused window, but it is the system
  # ANR surface, not a usable launcher or app. Treat that as still waking, dismiss it, and
  # force-stop the named package so the next attempt can reach a real window.
  focus="$("$adb" shell dumpsys window 2>/dev/null | grep -m 1 'mCurrentFocus' || true)"
  case "$focus" in
    *"Application Not Responding"*|*"com.google.android.googlesdksetup"*|*"com.google.android.setupwizard"*)
      echo "Dismissing emulator setup or ANR: ${focus## }"
      pkg="$(printf '%s\n' "$focus" | sed -n 's/.*Application Not Responding: \([^}]*\).*/\1/p' | tr -d '[:space:]')"
      if [ -n "$pkg" ]; then
        "$adb" shell am force-stop "$pkg" >/dev/null 2>&1 || true
      fi
      "$adb" shell am force-stop com.google.android.googlesdksetup >/dev/null 2>&1 || true
      "$adb" shell am force-stop com.google.android.setupwizard >/dev/null 2>&1 || true
      "$adb" shell input keyevent KEYCODE_ESCAPE >/dev/null 2>&1 || true
      "$adb" shell input keyevent KEYCODE_BACK >/dev/null 2>&1 || true
      "$adb" shell input keyevent KEYCODE_HOME >/dev/null 2>&1 || true
      ;;
  esac

  if node "$focus_helper" "$focus"; then
    echo "The emulator is awake and focused: ${focus## }"
    exit 0
  fi

  attempt=$((attempt + 1))
  sleep 2
done

echo "The emulator never reported a focused window; the suite will have to say what it finds." >&2
exit 0
