{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  packages = with pkgs; [
    git
    zip
    unzip
  ];

  shellHook = ''
    export AGENTUP_PACKAGING_TARGET=macos
    if [ "$(uname -s)" != "Darwin" ]; then
      echo "macOS packaging requires Darwin because hdiutil, pkgbuild, productbuild, codesign, and notarytool are Apple platform tools." >&2
      echo "Use this wrapper on a macOS runner or developer machine." >&2
      return 1 2>/dev/null || exit 1
    fi
  '';
}
