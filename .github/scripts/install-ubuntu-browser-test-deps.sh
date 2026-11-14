#!/usr/bin/env bash
set -euo pipefail

# Installs the WebKitGTK/Xvfb packages AgentUp.Tests needs on Ubuntu, then either
# points PUPPETEER_EXECUTABLE_PATH at a preinstalled Chrome or starts a background
# Chromium apt install so it can overlap the non-E2E GUI test run.

apt_install() {
  local attempt
  for attempt in 1 2 3; do
    if sudo apt-get install -y --no-install-recommends -o Acquire::Retries=3 "$@"; then
      return 0
    fi
    echo "apt install attempt $attempt/3 failed; repairing package state before retry"
    sudo dpkg --configure -a
    sudo apt-get -f install -y -o Acquire::Retries=3
    sleep $((attempt * 5))
  done
  return 1
}

# apt-cache policy always prints one Candidate line. The reader must consume the
# whole policy: grep -q under pipefail exits at the first match, apt-cache dies
# with SIGPIPE (141), and the pipeline looks false. Ubuntu 24.04 and 26.04 then
# fall through to libwebkit2gtk-4.0-37, which 26.04 no longer publishes.
read_webkit_candidate() {
  apt-cache policy libwebkit2gtk-4.1-0 | awk '/^[[:space:]]*Candidate:/{print $2}'
}

webkit_package_for_candidate() {
  local candidate="$1"
  if [ -n "$candidate" ] && [ "$candidate" != "(none)" ]; then
    printf '%s\n' libwebkit2gtk-4.1-0
  else
    printf '%s\n' libwebkit2gtk-4.0-37
  fi
}

install_browser_test_deps() {
  local candidate webkit_package webkit_lib chromium_exe
  sudo apt-get update -o Acquire::Retries=3
  apt_install xvfb x11-utils x11-apps dbus-x11 at-spi2-core libgtk-3-0 libxtst6
  candidate="$(read_webkit_candidate)"
  webkit_package="$(webkit_package_for_candidate "$candidate")"
  apt_install "$webkit_package"
  if [ "$webkit_package" = libwebkit2gtk-4.1-0 ]; then
    webkit_lib="$(ldconfig -p | awk '/libwebkit2gtk-4.1.so/{print $NF; exit}')"
  else
    webkit_lib="$(ldconfig -p | awk '/libwebkit2gtk-4.0.so/{print $NF; exit}')"
  fi
  if [ -n "$webkit_lib" ] && [ ! -e /usr/local/lib/libwebkit2gtk.so ]; then
    sudo ln -s "$webkit_lib" /usr/local/lib/libwebkit2gtk.so
    sudo ldconfig
  fi

  chromium_exe="$(command -v google-chrome-stable 2>/dev/null \
    || command -v google-chrome 2>/dev/null \
    || command -v chromium 2>/dev/null \
    || command -v chromium-browser 2>/dev/null \
    || true)"
  if [ -n "$chromium_exe" ]; then
    echo "PUPPETEER_EXECUTABLE_PATH=$chromium_exe" >> "$GITHUB_ENV"
    echo "Found pre-installed Chromium: $chromium_exe"
  else
    echo "No pre-installed Chromium found — installing in background"
    sudo apt-get install -y --no-install-recommends chromium > /tmp/chromium-install.log 2>&1 &
    printf '%s' "$!" > /tmp/chromium-install.pid
  fi
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  install_browser_test_deps
fi
