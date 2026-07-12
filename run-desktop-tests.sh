#!/usr/bin/env bash
# Run AgentUp.Desktop.Tests inside a nix-shell that provides native libraries
# required by Avalonia/SkiaSharp headless tests on NixOS.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/AgentUp.Desktop.Tests/AgentUp.Desktop.Tests.csproj"

exec nix-shell "$SCRIPT_DIR/shell.nix" \
    --run "dotnet test '$PROJECT' --logger 'console;verbosity=normal' $*"
