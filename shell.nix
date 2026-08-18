{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShell {
  # Native libraries required by Avalonia/SkiaSharp and WebKitGTK at runtime.
  # LD_LIBRARY_PATH is set below because dotnet loads these via dlopen,
  # not through the standard NixOS ld wrapper.
  # xorg.xvfb provides the virtual framebuffer X server used by E2E tests when
  # no real display is available (CI and headless environments).
  buildInputs = with pkgs; [
    nodejs_22
    alsa-lib
    at-spi2-atk
    cairo
    cups
    dbus
    expat
    fontconfig.lib
    freetype
    libGL
    libdrm
    libgbm
    libx11
    libxcb
    libxcomposite
    libxdamage
    libxext
    libxfixes
    libice
    libxkbcommon
    libxrandr
    libsm
    nspr
    nss
    pango
    patchelf
    stdenv.cc.cc
    systemd
    webkitgtk_4_1
    gtk3
    glib
    xvfb
    xdpyinfo
  ];

  # Expo downloads React Native DevTools as a generic Linux Electron binary.
  # NIX_LD lets that binary use the Nix-provided dynamic linker and libraries.
  NIX_LD = pkgs.stdenv.cc.bintools.dynamicLinker;
  NIX_LD_LIBRARY_PATH = pkgs.lib.makeLibraryPath (with pkgs; [
    alsa-lib
    at-spi2-atk
    cairo
    cups
    dbus
    expat
    fontconfig.lib
    freetype
    glib
    gtk3
    libGL
    libdrm
    libgbm
    libx11
    libxcb
    libxcomposite
    libxdamage
    libxext
    libxfixes
    libxkbcommon
    libxrandr
    nspr
    nss
    pango
    stdenv.cc.cc
    systemd
  ]);

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
      pkgs.alsa-lib
      pkgs.at-spi2-atk
      pkgs.cairo
      pkgs.cups
      pkgs.dbus
      pkgs.expat
      pkgs.libdrm
      pkgs.libgbm
      pkgs.libxcb
      pkgs.libxcomposite
      pkgs.libxdamage
      pkgs.libxext
      pkgs.libxfixes
      pkgs.libxkbcommon
      pkgs.libxrandr
      pkgs.nspr
      pkgs.nss
      pkgs.pango
      pkgs.stdenv.cc.cc
      pkgs.systemd
    ]}''${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

    # DotSlash downloads React Native DevTools after npm install. Patch its
    # Electron executables in the user cache so they use the Nix linker.
    for mobileRoot in "$PWD" "$PWD/AgentUp.Mobile"; do
      devtoolsManifest="$mobileRoot/node_modules/@react-native/debugger-shell/bin/react-native-devtools"
      dotslashTool="$mobileRoot/node_modules/.bin/dotslash"
      if [ -x "$dotslashTool" ] && [ -f "$devtoolsManifest" ]; then
        devtoolsExecutable="$($dotslashTool -- fetch "$devtoolsManifest")"
        chmod u+w "$devtoolsExecutable"
        patchelf --set-interpreter "$NIX_LD" "$devtoolsExecutable"

        crashpadHandler="$(dirname "$devtoolsExecutable")/chrome_crashpad_handler"
        if [ -x "$crashpadHandler" ]; then
          chmod u+w "$crashpadHandler"
          patchelf --set-interpreter "$NIX_LD" "$crashpadHandler"
        fi
        break
      fi
    done
  '';
}
