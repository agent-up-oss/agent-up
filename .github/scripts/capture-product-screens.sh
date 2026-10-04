#!/usr/bin/env bash
set -euo pipefail

# Captures every design-system page-assembly screen from the real Desktop and Mobile clients
# against their built-in Demo server, in a used state, and leaves the PNGs plus screens.json
# under artifacts/product-screens/ for the workflow to upload.
#
# No Agent-Up Server runs here. Demo is an in-process backend in each client, which is what
# makes this a screenshot job rather than a second end-to-end environment.

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repository_root"

echo "Building the clients au-debug will drive"
dotnet build AgentUp.Desktop/AgentUp.Desktop.csproj --configuration Debug
dotnet build AgentUp.AUDebug/AgentUp.AUDebug.csproj --configuration Debug

echo "Exporting the Mobile web client"
./.github/scripts/install-mobile-deps.sh AgentUp.Mobile
(cd AgentUp.Mobile && node scripts/export-web.mjs)

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
  dotnet run --project AgentUp.AUDebug -- screens

echo "Captured:"
find artifacts/product-screens -name '*.png' | sort
