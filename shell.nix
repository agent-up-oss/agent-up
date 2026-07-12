{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  # Native libraries required by Avalonia/SkiaSharp at runtime.
  # LD_LIBRARY_PATH is set below because dotnet loads these via dlopen,
  # not through the standard NixOS ld wrapper.
  buildInputs = with pkgs; [
    fontconfig.lib
    freetype
    libGL
    xorg.libX11
    xorg.libICE
    xorg.libSM
  ];

  shellHook = ''
    export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath [
      pkgs.fontconfig.lib
      pkgs.freetype
      pkgs.libGL
      pkgs.xorg.libX11
      pkgs.xorg.libICE
      pkgs.xorg.libSM
    ]}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
  '';
}
