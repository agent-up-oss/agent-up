#!/usr/bin/env bash
set -euo pipefail

# Captures every design-system page-assembly screen from the real Desktop and Mobile clients
# against their built-in Demo server, in a used state, and leaves the PNGs plus screens.json
# under artifacts/product-screens/ for the workflow to upload.
#
# No Agent-Up Server runs here. Demo is an in-process backend in each client, which is what
# makes this a screenshot job rather than a second end-to-end environment.
#
# With a surface argument only that client is built and captured. CI runs one surface per
# Showcase job: Mobile used to wait for its first paint on a runner Desktop capture had just
# saturated, which is where its flake came from.
#
#   ./.github/scripts/capture-product-screens.sh [desktop|mobile]

surface="${1:-}"
case "$surface" in
  ""|desktop|mobile) ;;
  *) echo "Usage: $0 [desktop|mobile]" >&2; exit 2 ;;
esac

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repository_root"

echo "Building the clients au-debug will drive"
dotnet build AgentUp.AUDebug/AgentUp.AUDebug.csproj --configuration Debug
if [ "$surface" != mobile ]; then
  dotnet build AgentUp.Desktop/AgentUp.Desktop.csproj --configuration Debug
fi

if [ "$surface" != desktop ]; then
  echo "Exporting the Mobile web client"
  ./.github/scripts/install-mobile-deps.sh AgentUp.Mobile
  (cd AgentUp.Mobile && node scripts/export-web.mjs)
fi

# The Desktop route points at a 1440x900 window, so the virtual screen has to be at least
# that; xdotool sizes the window itself once it is mapped.
echo "Capturing product screens"
LIBGL_ALWAYS_SOFTWARE=1 \
  GALLIUM_DRIVER=llvmpipe \
  WEBKIT_DISABLE_COMPOSITING_MODE=1 \
  WEBKIT_DISABLE_DMABUF_RENDERER=1 \
  WEBKIT_DISABLE_SANDBOX_THIS_IS_DANGEROUS=1 \
  dbus-run-session xvfb-run -a \
  --server-args="-screen 0 1440x900x24 -ac +extension GLX +render -noreset" \
  dotnet run --project AgentUp.AUDebug -- screens ${surface:+"$surface"}

echo "Captured:"
find artifacts/product-screens -name '*.png' | sort
