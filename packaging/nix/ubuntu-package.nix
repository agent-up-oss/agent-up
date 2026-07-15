{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  packages = with pkgs; [
    dpkg
    fakeroot
    file
    git
    zip
    unzip
  ];

  shellHook = ''
    export AGENTUP_PACKAGING_TARGET=ubuntu
  '';
}
