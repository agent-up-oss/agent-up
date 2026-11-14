#!/usr/bin/env bash
# Proves WebKitGTK 4.1 stays selected when apt-cache keeps writing after the
# Candidate line. grep -q under pipefail used to report that package as absent.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=install-ubuntu-browser-test-deps.sh
source "$root/.github/scripts/install-ubuntu-browser-test-deps.sh"

fail() {
  printf 'install-ubuntu-browser-test-deps.test: %s\n' "$1" >&2
  exit 1
}

with_apt_cache() {
  local dir="$1"
  PATH="$dir:$PATH" read_webkit_candidate
}

dir="$(mktemp -d)"
trap 'rm -rf "$dir"' EXIT
cat > "$dir/apt-cache" <<'EOF'
#!/bin/sh
printf '%s\n' 'libwebkit2gtk-4.1-0:' '  Installed: (none)' '  Candidate: 2.52.0-1' '  Version table:'
# Extra output after the candidate. An early-exit reader SIGPIPEs this writer.
printf '%s\n' '     2.52.0-1 500' '        500 http://archive.ubuntu.com/ubuntu resolute/main amd64 Packages'
EOF
chmod +x "$dir/apt-cache"

candidate="$(with_apt_cache "$dir")"
[ "$candidate" = "2.52.0-1" ] || fail "expected 4.1 candidate, got '$candidate'"
[ "$(webkit_package_for_candidate "$candidate")" = libwebkit2gtk-4.1-0 ] || fail "4.1 candidate selected the 4.0 package"

cat > "$dir/apt-cache" <<'EOF'
#!/bin/sh
printf '%s\n' 'libwebkit2gtk-4.1-0:' '  Installed: (none)' '  Candidate: (none)' '  Version table:'
EOF
chmod +x "$dir/apt-cache"
candidate="$(with_apt_cache "$dir")"
[ "$candidate" = "(none)" ] || fail "expected no 4.1 candidate, got '$candidate'"
[ "$(webkit_package_for_candidate "$candidate")" = libwebkit2gtk-4.0-37 ] || fail "missing 4.1 candidate did not select 4.0"

if command -v /usr/bin/apt-cache >/dev/null 2>&1; then
  real="$(PATH="/usr/bin:$PATH" read_webkit_candidate)"
  case "$real" in
    "" ) fail "real apt-cache printed no candidate" ;;
    "(none)") webkit_package_for_candidate "$real" >/dev/null ;;
    *) [ "$(webkit_package_for_candidate "$real")" = libwebkit2gtk-4.1-0 ] || fail "real candidate '$real' did not select 4.1" ;;
  esac
fi
