{ pkgs ? import <nixpkgs> {} }:

let
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
  ++ optional "osslsigncode"
  ++ optional "wine";

  shellHook = ''
    export AGENTUP_PACKAGING_TARGET=windows
  '';
}
