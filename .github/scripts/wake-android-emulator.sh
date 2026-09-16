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
# A condition rather than a sleep, deliberately: a fixed delay long enough for a slow runner is
# waste on a fast one, and short enough for a fast one is a test that fails on a slow one.
set -euo pipefail

adb="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}/platform-tools/adb"
if [ ! -x "$adb" ]; then
  adb="$(command -v adb || true)"
fi
if [ -z "$adb" ]; then
  echo "No adb on this machine, so there is no emulator to wake; leaving it to the suite." >&2
  exit 0
fi

"$adb" wait-for-device

attempt=1
while [ "$attempt" -le 60 ]; do
  # Both are best-effort: on a device that is already awake and unlocked they change nothing, and
  # neither is worth failing the run over.
  "$adb" shell input keyevent KEYCODE_WAKEUP >/dev/null 2>&1 || true
  "$adb" shell wm dismiss-keyguard >/dev/null 2>&1 || true

  # mCurrentFocus is null while nothing holds focus, which is the state Espresso gives up on.
  focus="$("$adb" shell dumpsys window 2>/dev/null | grep -m 1 'mCurrentFocus' || true)"
  case "$focus" in
    *"mCurrentFocus=null"*|"") ;;
    *) echo "The emulator is awake and focused: ${focus## }"; exit 0 ;;
  esac

  attempt=$((attempt + 1))
  sleep 2
done

echo "The emulator never reported a focused window; the suite will have to say what it finds." >&2
exit 0
