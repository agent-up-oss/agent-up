#!/usr/bin/env bash
# Run AgentUp.InstallerApp inside a nix-shell that provides the native libraries
# Avalonia/SkiaSharp needs on NixOS (fontconfig, freetype, libGL, X11).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/AgentUp.InstallerApp/AgentUp.InstallerApp.csproj"

export AGENTUP_INSTALLER_NIXOS_LOOKUP_ONLY="${AGENTUP_INSTALLER_NIXOS_LOOKUP_ONLY:-1}"

exec nix-shell "$SCRIPT_DIR/shell.nix" \
    --run "dotnet run --project '$PROJECT'"
