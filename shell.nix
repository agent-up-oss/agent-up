{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  # Native libraries required by Avalonia/SkiaSharp and WebKitGTK at runtime.
  # LD_LIBRARY_PATH is set below because dotnet loads these via dlopen,
  # not through the standard NixOS ld wrapper.
  buildInputs = with pkgs; [
    fontconfig.lib
    freetype
    libGL
    xorg.libX11
    xorg.libICE
    xorg.libSM
    webkitgtk_4_1
    gtk3
    glib
  ];

  shellHook = ''
    export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath [
      pkgs.fontconfig.lib
      pkgs.freetype
      pkgs.libGL
      pkgs.xorg.libX11
      pkgs.xorg.libICE
      pkgs.xorg.libSM
      pkgs.webkitgtk_4_1
      pkgs.gtk3
      pkgs.glib
    ]}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
  '';
}
