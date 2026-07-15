{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  # Native libraries required by Avalonia/SkiaSharp and WebKitGTK at runtime.
  # LD_LIBRARY_PATH is set below because dotnet loads these via dlopen,
  # not through the standard NixOS ld wrapper.
  # xorg.xvfb provides the virtual framebuffer X server used by E2E tests when
  # no real display is available (CI and headless environments).
  buildInputs = with pkgs; [
    fontconfig.lib
    freetype
    libGL
    libx11
    libice
    libsm
    webkitgtk_4_1
    gtk3
    glib
    xvfb
    xdpyinfo
  ];

  shellHook = ''
    export LD_LIBRARY_PATH="${pkgs.lib.makeLibraryPath [
      pkgs.fontconfig.lib
      pkgs.freetype
      pkgs.libGL
      pkgs.libx11
      pkgs.libice
      pkgs.libsm
      pkgs.webkitgtk_4_1
      pkgs.gtk3
      pkgs.glib
    ]}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
  '';
}
