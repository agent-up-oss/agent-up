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
    xorg.libXtst
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
    xorg.xclock
    xdpyinfo
    curl
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
    xorg.libXtst
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

    # Workspace ACP adapters are not hardcoded in C#. Cache them locally and
    # declare command/arguments in a repo inventory shared by Server and Desktop.
    devRoot="$PWD/.agent-up-dev"
    mkdir -p "$devRoot/bin" "$devRoot/npm" "$devRoot/cursor-agent"
    if [ ! -d "$devRoot/npm/node_modules/@agentclientprotocol/codex-acp" ] \
       || [ ! -d "$devRoot/npm/node_modules/@agentclientprotocol/claude-agent-acp" ]; then
      echo "Installing Codex and Claude ACP adapters into .agent-up-dev/npm ..."
      rm -rf "$devRoot/npm/node_modules" "$devRoot/npm/package-lock.json" "$devRoot/npm/package.json"
      npm install --omit=dev --prefix "$devRoot/npm" \
        @agentclientprotocol/codex-acp \
        @agentclientprotocol/claude-agent-acp \
        || echo "warning: npm ACP adapter install failed; Codex/Claude discovery will stay empty until it succeeds"
    fi
    if [ ! -x "$devRoot/bin/agent" ] && [ ! -x "$devRoot/cursor-agent/cursor-agent" ]; then
      echo "Installing Cursor Agent CLI into .agent-up-dev/cursor-agent ..."
      os="$(uname -s)"
      arch="$(uname -m)"
      case "$os" in Linux*) OS=linux ;; Darwin*) OS=darwin ;; *) OS="" ;; esac
      case "$arch" in x86_64|amd64) ARCH=x64 ;; arm64|aarch64) ARCH=arm64 ;; *) ARCH="" ;; esac
      if [ -n "$OS" ] && [ -n "$ARCH" ]; then
        download_url="$(curl -fsSL https://cursor.com/install | grep '^DOWNLOAD_URL=' | head -n1 | cut -d= -f2- | tr -d '"')"
        case "$download_url" in
          https://*) ;;
          *) download_url="" ;;
        esac
        if printf '%s' "$download_url" | grep -Eq '[[:space:];|&`$()]'; then
          download_url=""
        fi
        if [ -n "$download_url" ] && curl -fsSL "$download_url" | tar --strip-components=1 -xzf - -C "$devRoot/cursor-agent"; then
          :
        else
          echo "warning: Cursor Agent CLI download failed; Cursor discovery will stay empty until it succeeds"
        fi
      fi
    fi
    if [ -x "$devRoot/cursor-agent/cursor-agent" ]; then
      ln -sfn "$devRoot/cursor-agent/cursor-agent" "$devRoot/bin/agent"
    fi
    if [ -n "''${NIX_LD-}" ] && command -v patchelf >/dev/null; then
      find "$devRoot" -type f -executable 2>/dev/null | while read -r bin; do
        if patchelf --print-interpreter "$bin" >/dev/null 2>&1; then
          chmod u+w "$bin" 2>/dev/null || true
          patchelf --set-interpreter "$NIX_LD" "$bin" 2>/dev/null || true
        fi
      done
    fi
    export AGENT_UP_DEV_ROOT="$devRoot"
    export PATH="$devRoot/bin:$devRoot/npm/node_modules/.bin''${PATH:+:$PATH}"
    node -e "
      const fs = require('fs');
      const path = require('path');
      const root = process.env.AGENT_UP_DEV_ROOT;
      function resolve(file) {
        return fs.existsSync(file) ? path.resolve(file) : null;
      }
      const entries = [];
      const codex = resolve(path.join(root, 'npm/node_modules/.bin/codex-acp'));
      const cursor = resolve(path.join(root, 'bin/agent'));
      const claude = resolve(path.join(root, 'npm/node_modules/.bin/claude-agent-acp'));
      if (codex) entries.push({ id: 'codex', versions: ['dev'], command: codex, arguments: [] });
      if (cursor) entries.push({ id: 'cursor', versions: ['dev'], command: cursor, arguments: ['acp'] });
      if (claude) entries.push({ id: 'claude', versions: ['dev'], command: claude, arguments: [] });
      fs.writeFileSync(path.join(root, 'capabilities.json'), JSON.stringify(entries, null, 2) + '\n');
    " || echo "warning: failed to write .agent-up-dev/capabilities.json"
    export AGENTUP_CAPABILITY_INVENTORY_PATH="$devRoot/capabilities.json"
  '';
}
