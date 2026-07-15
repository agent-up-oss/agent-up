{ pkgs ? import <nixpkgs> {} }:

let
  repoRoot = toString ../..;
  optional = name:
    if builtins.hasAttr name pkgs then [ (builtins.getAttr name pkgs) ] else [];
in
pkgs.mkShell {
  packages = with pkgs; [
    git
    zip
    unzip
    p7zip
  ]
  ++ optional "msitools"
  ++ optional "osslsigncode";

  shellHook = ''
    export AGENTUP_REPO_ROOT="${repoRoot}"
    export AGENTUP_PACKAGING_TARGET=windows
    export DOTNET_CLI_HOME="$AGENTUP_REPO_ROOT/.dotnet"
    export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    export DOTNET_NOLOGO=1

    mkdir -p "$DOTNET_CLI_HOME" "$AGENTUP_REPO_ROOT/artifacts/tools/windows/bin" "$AGENTUP_REPO_ROOT/artifacts/tools/windows/home"
    dotnet tool restore --tool-manifest "$AGENTUP_REPO_ROOT/packaging/windows/dotnet-tools.json"

    cat > "$AGENTUP_REPO_ROOT/artifacts/tools/windows/bin/wix" <<'WIXSHIM'
#!/usr/bin/env bash
set -euo pipefail
export HOME="$AGENTUP_REPO_ROOT/artifacts/tools/windows/home"
cd "$AGENTUP_REPO_ROOT/packaging/windows"
exec dotnet tool run wix -- "$@"
WIXSHIM
    chmod +x "$AGENTUP_REPO_ROOT/artifacts/tools/windows/bin/wix"
    export PATH="$AGENTUP_REPO_ROOT/artifacts/tools/windows/bin:$PATH"
  '';
}
