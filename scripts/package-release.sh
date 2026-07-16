#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 <platform> <runtime-id> <version> [output-dir] [--payload-root <path>]" >&2
  echo "Platforms: ubuntu, nixos, macos, windows" >&2
}

if [ "$#" -lt 3 ] || [ "$#" -gt 6 ]; then
  usage
  exit 2
fi

platform="$1"
rid="$2"
version="$3"
shift 3
output_dir="artifacts"
payload_root="${AGENTUP_PACKAGE_PAYLOAD_ROOT:-}"
configuration="${CONFIGURATION:-Release}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage="$root/artifacts/stage/$platform-$rid"

if [ "$#" -gt 0 ] && [ "$1" != "--payload-root" ]; then
  output_dir="$1"
  shift
fi

if [ "$#" -gt 0 ]; then
  if [ "$#" -ne 2 ] || [ "$1" != "--payload-root" ]; then
    usage
    exit 2
  fi

  payload_root="$2"
  shift 2
fi

if [ "$#" -ne 0 ]; then
  usage
  exit 2
fi

ensure_wix_cli() {
  export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-$root/.dotnet}"
  mkdir -p "$DOTNET_CLI_HOME" "$root/artifacts/tools/windows/bin" "$root/artifacts/tools/windows/home"
  dotnet tool restore --tool-manifest "$root/packaging/windows/dotnet-tools.json"

  dotnet_cli_home_cmd="$DOTNET_CLI_HOME"
  root_cmd="$root"
  home_cmd="$root/artifacts/tools/windows/home"
  wix_command="$root/artifacts/tools/windows/bin/wix"
  wix_command_cmd="$root/artifacts/tools/windows/bin/wix.cmd"
  if command -v cygpath >/dev/null 2>&1; then
    dotnet_cli_home_cmd="$(cygpath -w "$DOTNET_CLI_HOME")"
    root_cmd="$(cygpath -w "$root")"
    home_cmd="$(cygpath -w "$root/artifacts/tools/windows/home")"
    wix_command_cmd="$(cygpath -w "$root/artifacts/tools/windows/bin/wix.cmd")"
  fi

cat > "$root/artifacts/tools/windows/bin/wix" <<WIXSHIM
#!/usr/bin/env bash
set -euo pipefail
export DOTNET_CLI_HOME="$DOTNET_CLI_HOME"
export HOME="$root/artifacts/tools/windows/home"
cd "$root/packaging/windows"
exec dotnet tool run wix -- "\$@"
WIXSHIM

cat > "$root/artifacts/tools/windows/bin/wix.cmd" <<WIXCMDSHIM
@echo off
set DOTNET_CLI_HOME=$dotnet_cli_home_cmd
set HOME=$home_cmd
cd /d "$root_cmd\packaging\windows"
dotnet tool run wix -- %*
WIXCMDSHIM
  chmod +x "$root/artifacts/tools/windows/bin/wix"
  export PATH="$root/artifacts/tools/windows/bin:$PATH"
  if [ "${OS:-}" = "Windows_NT" ]; then
    export AGENTUP_WIX_COMMAND="$wix_command_cmd"
  else
    export AGENTUP_WIX_COMMAND="$wix_command"
  fi
  "$wix_command" extension add WixToolset.Bal.wixext
}

if [ "$platform" = "ubuntu" ] || [ "$platform" = "macos" ] || [ "$platform" = "windows" ]; then
  if [ "$platform" = "windows" ]; then
    ensure_wix_cli
  fi

  packaging_command=("$root/AgentUp.Packaging/bin/$configuration/net10.0/AgentUp.Packaging")
  if [ -n "${AGENTUP_PACKAGING_COMMAND:-}" ]; then
    packaging_command=("$AGENTUP_PACKAGING_COMMAND")
  elif [ ! -x "${packaging_command[0]}" ]; then
    packaging_command=(dotnet run --project "$root/AgentUp.Packaging/AgentUp.Packaging.csproj" --configuration "$configuration" --)
  fi

  package_args=(package "$platform" "$rid" "$version" "$output_dir")
  if [ -n "$payload_root" ]; then
    package_args+=(--payload-root "$payload_root")
  fi

  export AGENTUP_REPOSITORY_ROOT="$root"
  "${packaging_command[@]}" "${package_args[@]}"
  exit 0
fi

rm -rf "$stage"
mkdir -p "$stage/desktop" "$stage/server" "$stage/cli" "$root/$output_dir"

if [ -n "$payload_root" ]; then
  cp -a "$payload_root/desktop/." "$stage/desktop/"
  cp -a "$payload_root/server/." "$stage/server/"
  cp -a "$payload_root/cli/." "$stage/cli/"
else
  dotnet restore "$root/AgentUp.Desktop/AgentUp.Desktop.csproj" --runtime "$rid"
  dotnet publish "$root/AgentUp.Desktop/AgentUp.Desktop.csproj" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:Version="$version" \
    -o "$stage/desktop"

  dotnet restore "$root/AgentUp.Server/AgentUp.Server.csproj" --runtime "$rid"
  dotnet publish "$root/AgentUp.Server/AgentUp.Server.csproj" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:Version="$version" \
    -o "$stage/server"

  dotnet restore "$root/AgentUp.CLI/AgentUp.CLI.csproj" --runtime "$rid"
  dotnet publish "$root/AgentUp.CLI/AgentUp.CLI.csproj" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:Version="$version" \
    -o "$stage/cli"
fi

case "$platform" in
  nixos)
    pkgs_root="$stage/nixos-pkgs"
    mkdir -p "$pkgs_root/package/opt/agent-up"
    cp -a "$stage/desktop" "$pkgs_root/package/opt/agent-up/desktop"
    cp -a "$stage/server" "$pkgs_root/package/opt/agent-up/server"
    cp -a "$stage/cli" "$pkgs_root/package/opt/agent-up/cli"
    cp -a "$root/media/logo.png" "$pkgs_root/package/opt/agent-up/logo.png"
    cat > "$pkgs_root/flake.nix" <<'NIX'
{
  description = "Agent-Up package set";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      systems = [ "x86_64-linux" ];
      forAllSystems = nixpkgs.lib.genAttrs systems;
      packageFor = pkgs: pkgs.stdenv.mkDerivation {
        pname = "agent-up";
        version = "@AGENT_UP_VERSION@";
        src = ./package;
        nativeBuildInputs = [
          pkgs.autoPatchelfHook
          pkgs.makeWrapper
        ];
        autoPatchelfIgnoreMissingDeps = [
          "liblttng-ust.so.0"
        ];
        buildInputs = [
          pkgs.fontconfig.lib
          pkgs.freetype
          pkgs.glib
          pkgs.gtk3
          pkgs.icu
          pkgs.libGL
          pkgs.libice
          pkgs.libsm
          pkgs.libx11
          pkgs.lttng-ust
          pkgs.openssl
          pkgs.stdenv.cc.cc.lib
          pkgs.webkitgtk_4_1
          pkgs.zlib
        ];
        dontConfigure = true;
        dontBuild = true;
        installPhase = ''
          runHook preInstall
          mkdir -p $out
          cp -R opt $out/
          chmod +x $out/opt/agent-up/desktop/AgentUp.Desktop
          chmod +x $out/opt/agent-up/server/AgentUp.Server
          chmod +x $out/opt/agent-up/cli/AgentUp.CLI
          mkdir -p $out/bin
          ln -s $out/opt/agent-up/desktop/AgentUp.Desktop $out/bin/agent-up-desktop
          ln -s $out/opt/agent-up/server/AgentUp.Server $out/bin/agent-up-server
          ln -s $out/opt/agent-up/cli/AgentUp.CLI $out/bin/agent-up
          runHook postInstall
        '';
        postFixup = ''
          runtime_libs="${pkgs.lib.makeLibraryPath [
            pkgs.fontconfig.lib
            pkgs.freetype
            pkgs.glib
            pkgs.gtk3
            pkgs.icu
            pkgs.libGL
            pkgs.libice
            pkgs.libsm
            pkgs.libx11
            pkgs.lttng-ust
            pkgs.openssl
            pkgs.stdenv.cc.cc.lib
            pkgs.webkitgtk_4_1
            pkgs.zlib
          ]}"

          wrapProgram $out/opt/agent-up/desktop/AgentUp.Desktop \
            --prefix LD_LIBRARY_PATH : "$runtime_libs"
          wrapProgram $out/opt/agent-up/server/AgentUp.Server \
            --prefix LD_LIBRARY_PATH : "$runtime_libs"
          wrapProgram $out/opt/agent-up/cli/AgentUp.CLI \
            --prefix LD_LIBRARY_PATH : "$runtime_libs"
        '';
      };
    in
    {
      packages = forAllSystems (system:
        let
          pkgs = import nixpkgs { inherit system; };
        in
        {
          agent-up = packageFor pkgs;
          default = self.packages.${system}.agent-up;
        });

      overlays.default = final: prev: {
        agent-up = self.packages.${final.system}.agent-up;
      };

      nixosModules.default = { config, lib, pkgs, ... }:
        let
          cfg = config.services.agent-up;
          package = self.packages.${pkgs.system}.agent-up;
        in
        {
          options.services.agent-up = {
            enable = lib.mkEnableOption "agent-up server";
            port = lib.mkOption {
              type = lib.types.port;
              default = 5000;
              description = "Loopback port for the Agent-Up server.";
            };
            dataDir = lib.mkOption {
              type = lib.types.str;
              default = "/var/lib/agent-up";
              description = "Directory used by Agent-Up for persistent server data.";
            };
          };

          config = lib.mkIf cfg.enable {
            environment.systemPackages = [ package ];
            systemd.services.agent-up = {
              description = "Agent-Up Server";
              wantedBy = [ "multi-user.target" ];
              after = [ "network.target" ];
              serviceConfig = {
                ExecStart = "${package}/bin/agent-up-server --urls http://127.0.0.1:${toString cfg.port}";
                Restart = "on-failure";
                RestartSec = 5;
                StateDirectory = "agent-up";
                Environment = [
                  "ASPNETCORE_URLS=http://127.0.0.1:${toString cfg.port}"
                  "Storage__DataDirectory=${cfg.dataDir}"
                ];
              };
            };
          };
        };

      homeManagerModules.default = { config, lib, pkgs, ... }:
        let
          cfg = config.programs.agent-up;
          package = self.packages.${pkgs.system}.agent-up;
        in
        {
          options.programs.agent-up.enable = lib.mkEnableOption "agent-up desktop";

          config = lib.mkIf cfg.enable {
            home.packages = [ package ];
            home.file.".local/share/icons/hicolor/256x256/apps/agent-up.png".source =
              "${package}/opt/agent-up/logo.png";
            xdg.desktopEntries.agent-up = {
              name = "Agent Up";
              exec = "agent-up-desktop";
              icon = "agent-up";
              terminal = false;
              categories = [ "Utility" ];
            };
          };
        };
    };
}
NIX
    sed -i.bak "s|@AGENT_UP_VERSION@|${version#v}|g" "$pkgs_root/flake.nix"
    rm -f "$pkgs_root/flake.nix.bak"
    nix --extra-experimental-features "nix-command flakes" flake lock "path:$pkgs_root"
    tar -C "$pkgs_root" -czf "$root/$output_dir/agent-up-nixos-pkgs.tar.gz" .
    ;;
  *)
    usage
    exit 2
    ;;
esac
