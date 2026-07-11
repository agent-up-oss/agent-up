#!/usr/bin/env bash
# Run AgentUp.Desktop inside a nix-shell that provides the native libraries
# Avalonia/SkiaSharp needs on NixOS (fontconfig, freetype, libGL, X11).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/AgentUp.Desktop/AgentUp.Desktop.csproj"

exec nix-shell "$SCRIPT_DIR/shell.nix" \
    --run "dotnet run --project '$PROJECT'"
