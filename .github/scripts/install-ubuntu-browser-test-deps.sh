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

sudo apt-get update -o Acquire::Retries=3
apt_install xvfb x11-utils x11-apps dbus-x11 at-spi2-core libgtk-3-0 libxtst6
if apt-cache policy libwebkit2gtk-4.1-0 | grep -q 'Candidate: [^()]'; then
  apt_install libwebkit2gtk-4.1-0
  webkit_lib="$(ldconfig -p | awk '/libwebkit2gtk-4.1.so/{print $NF; exit}')"
else
  apt_install libwebkit2gtk-4.0-37
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
