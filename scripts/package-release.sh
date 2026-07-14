#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 <platform> <runtime-id> <version> [output-dir]" >&2
  echo "Platforms: ubuntu, nixos, macos, windows" >&2
}

if [ "$#" -lt 3 ] || [ "$#" -gt 4 ]; then
  usage
  exit 2
fi

platform="$1"
rid="$2"
version="$3"
output_dir="${4:-artifacts}"
configuration="${CONFIGURATION:-Release}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage="$root/artifacts/stage/$platform-$rid"

create_zip() {
  local output="$1"
  shift

  if [ "${RUNNER_OS:-}" = "Windows" ] && command -v powershell.exe >/dev/null 2>&1; then
    local ps_output="$output"
    if command -v cygpath >/dev/null 2>&1; then
      ps_output="$(cygpath -w "$output")"
    fi

    local ps_items=()
    for item in "$@"; do
      local ps_item="$item"
      if command -v cygpath >/dev/null 2>&1; then
        ps_item="$(cygpath -w "$item")"
      fi
      ps_items+=("'${ps_item//\'/\'\'}'")
    done

    local joined
    joined="$(IFS=,; echo "${ps_items[*]}")"
    powershell.exe -NoProfile -Command "\$items = @($joined); \$destination = '$ps_output'; New-Item -ItemType Directory -Force -Path (Split-Path -Parent \$destination) | Out-Null; Compress-Archive -LiteralPath \$items -DestinationPath \$destination -Force"
  elif command -v zip >/dev/null 2>&1; then
    zip -qr "$output" "$@"
  elif tar --help 2>/dev/null | grep -q -- '-a'; then
    tar -a -cf "$output" "$@"
  else
    echo "zip or tar -a is required to create $output" >&2
    exit 1
  fi
}

create_windows_installer() {
  local payload_zip="$1"
  local output_exe="$2"
  local installer_src="$stage/installer-src"
  local installer_publish="$stage/installer-publish"

  rm -rf "$installer_src" "$installer_publish"
  mkdir -p "$installer_src"
  cp "$payload_zip" "$installer_src/payload.zip"

  cat > "$installer_src/AgentUp.Installer.csproj" <<'CSPROJ'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <EmbeddedResource Include="payload.zip" LogicalName="payload.zip" />
  </ItemGroup>
</Project>
CSPROJ

  cat > "$installer_src/Program.cs" <<'CS'
using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;

var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Agent-Up");
var extractOnly = false;
var quiet = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--extract":
            extractOnly = true;
            installDir = args[++i];
            break;
        case "--install-dir":
            installDir = args[++i];
            break;
        case "--quiet":
            quiet = true;
            break;
        default:
            throw new ArgumentException($"Unknown argument: {args[i]}");
    }
}

Directory.CreateDirectory(installDir);

await using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
    ?? throw new InvalidOperationException("Embedded payload.zip was not found.");
using (var archive = new ZipArchive(payload, ZipArchiveMode.Read))
{
    archive.ExtractToDirectory(installDir, overwriteFiles: true);
}

if (!extractOnly)
{
    var installScript = Path.Combine(installDir, "tools", "install-agent-up-server.ps1");
    var startInfo = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add("-NoProfile");
    startInfo.ArgumentList.Add("-ExecutionPolicy");
    startInfo.ArgumentList.Add("Bypass");
    startInfo.ArgumentList.Add("-File");
    startInfo.ArgumentList.Add(installScript);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
    {
        return process.ExitCode;
    }
}

if (!quiet)
{
    Console.WriteLine($"Agent-Up installed to {installDir}.");
    Console.WriteLine(Path.Combine(installDir, "desktop", "AgentUp.Desktop.exe"));
}

return 0;
CS

  dotnet publish "$installer_src/AgentUp.Installer.csproj" \
    --configuration "$configuration" \
    --runtime "$rid" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:Version="$version" \
    -o "$installer_publish"

  cp "$installer_publish/AgentUp.Installer.exe" "$output_exe"
}

rm -rf "$stage"
mkdir -p "$stage/desktop" "$stage/server" "$stage/cli" "$root/$output_dir"

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

case "$platform" in
  macos)
    app="$stage/Agent-Up.app"
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources/server"
    cp -R "$stage/desktop/." "$app/Contents/MacOS/"
    cp -R "$stage/server/." "$app/Contents/Resources/server/"
    cat > "$app/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>Agent-Up</string>
  <key>CFBundleDisplayName</key>
  <string>Agent-Up</string>
  <key>CFBundleIdentifier</key>
  <string>dev.agent-up.desktop</string>
  <key>CFBundleExecutable</key>
  <string>AgentUp.Desktop</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
</dict>
</plist>
PLIST
    cp "$root/packaging/macos/agent-up-server.plist" "$stage/agent-up-server.plist"
    cat > "$stage/install.sh" <<'INSTALL'
#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sudo rm -rf /Applications/Agent-Up.app
sudo cp -R "$root/Agent-Up.app" /Applications/Agent-Up.app
sudo chmod +x /Applications/Agent-Up.app/Contents/MacOS/AgentUp.Desktop
sudo chmod +x /Applications/Agent-Up.app/Contents/Resources/server/AgentUp.Server
sudo mkdir -p "/Library/Application Support/Agent-Up"
sudo cp "$root/agent-up-server.plist" /Library/LaunchDaemons/dev.agent-up.server.plist
sudo chown root:wheel /Library/LaunchDaemons/dev.agent-up.server.plist
sudo chmod 644 /Library/LaunchDaemons/dev.agent-up.server.plist
sudo launchctl bootout system /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true
sudo launchctl bootstrap system /Library/LaunchDaemons/dev.agent-up.server.plist
sudo launchctl kickstart -k system/dev.agent-up.server
echo "Agent-Up installed. Start Desktop from /Applications/Agent-Up.app"
INSTALL
    chmod +x "$stage/install.sh"
    cat > "$stage/uninstall.sh" <<'UNINSTALL'
#!/usr/bin/env bash
set -euo pipefail
sudo launchctl bootout system /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true
sudo rm -f /Library/LaunchDaemons/dev.agent-up.server.plist
sudo rm -rf "/Library/Application Support/Agent-Up"
sudo rm -rf /Applications/Agent-Up.app
UNINSTALL
    chmod +x "$stage/uninstall.sh"
    dmg_root="$stage/dmg-root"
    mkdir -p "$dmg_root"
    cp -R "$stage/Agent-Up.app" "$dmg_root/"
    cp -R "$stage/cli" "$dmg_root/"
    cp "$stage/agent-up-server.plist" "$dmg_root/"
    cp "$stage/install.sh" "$dmg_root/"
    cp "$stage/uninstall.sh" "$dmg_root/"
    if command -v hdiutil >/dev/null 2>&1; then
      hdiutil create -volname "Agent-Up" -srcfolder "$dmg_root" -ov -format UDZO "$root/$output_dir/agent-up-macos-$rid.dmg"
    else
      echo "hdiutil is required to create macOS DMG artifacts" >&2
      exit 1
    fi
    ;;
  windows)
    mkdir -p "$stage/tools"
    cp "$root/packaging/windows/install-agent-up-server.ps1" "$stage/tools/"
    cp "$root/packaging/windows/uninstall-agent-up-server.ps1" "$stage/tools/"
    (cd "$stage" && create_zip "$stage/payload.zip" desktop server cli tools)
    create_windows_installer "$stage/payload.zip" "$root/$output_dir/agent-up-windows-$rid.exe"
    ;;
  ubuntu)
    mkdir -p "$stage/share"
    cp "$root/packaging/linux/agent-up-server.service" "$stage/share/"
    cat > "$stage/install.sh" <<'INSTALL'
#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sudo mkdir -p /opt/agent-up
sudo mkdir -p /var/lib/agent-up
sudo touch /var/log/agent-up-server.log /var/log/agent-up-server.err.log
sudo rm -rf /opt/agent-up/desktop /opt/agent-up/server /opt/agent-up/cli
sudo cp -a "$root/desktop" /opt/agent-up/desktop
sudo cp -a "$root/server" /opt/agent-up/server
sudo cp -a "$root/cli" /opt/agent-up/cli
sudo chmod +x /opt/agent-up/desktop/AgentUp.Desktop /opt/agent-up/server/AgentUp.Server /opt/agent-up/cli/AgentUp.CLI
sudo cp "$root/share/agent-up-server.service" /etc/systemd/system/agent-up-server.service
sudo systemctl daemon-reload
sudo systemctl enable --now agent-up-server.service
echo "Agent-Up installed. Start Desktop with /opt/agent-up/desktop/AgentUp.Desktop"
INSTALL
    chmod +x "$stage/install.sh"
    cat > "$stage/uninstall.sh" <<'UNINSTALL'
#!/usr/bin/env bash
set -euo pipefail
sudo systemctl disable --now agent-up-server.service || true
sudo rm -f /etc/systemd/system/agent-up-server.service
sudo systemctl daemon-reload
sudo rm -rf /opt/agent-up
UNINSTALL
    chmod +x "$stage/uninstall.sh"
    deb_root="$stage/deb-root"
    mkdir -p "$deb_root/DEBIAN" "$deb_root/opt/agent-up" "$deb_root/etc/systemd/system"
    cp -a "$stage/desktop" "$deb_root/opt/agent-up/desktop"
    cp -a "$stage/server" "$deb_root/opt/agent-up/server"
    cp -a "$stage/cli" "$deb_root/opt/agent-up/cli"
    cp "$stage/share/agent-up-server.service" "$deb_root/etc/systemd/system/agent-up-server.service"
    chmod +x "$deb_root/opt/agent-up/desktop/AgentUp.Desktop" "$deb_root/opt/agent-up/server/AgentUp.Server" "$deb_root/opt/agent-up/cli/AgentUp.CLI"
    deb_version="${version#v}"
    cat > "$deb_root/DEBIAN/control" <<CONTROL
Package: agent-up
Version: $deb_version
Section: devel
Priority: optional
Architecture: amd64
Maintainer: Agent-Up <ci@agent-up.local>
Description: Local Agent-Up desktop, CLI, and server service.
CONTROL
    cat > "$deb_root/DEBIAN/postinst" <<'POSTINST'
#!/usr/bin/env bash
set -e
mkdir -p /var/lib/agent-up
touch /var/log/agent-up-server.log /var/log/agent-up-server.err.log
chmod +x /opt/agent-up/desktop/AgentUp.Desktop /opt/agent-up/server/AgentUp.Server /opt/agent-up/cli/AgentUp.CLI
systemctl daemon-reload
systemctl enable --now agent-up-server.service
POSTINST
    cat > "$deb_root/DEBIAN/prerm" <<'PRERM'
#!/usr/bin/env bash
set -e
systemctl disable --now agent-up-server.service 2>/dev/null || true
PRERM
    cat > "$deb_root/DEBIAN/postrm" <<'POSTRM'
#!/usr/bin/env bash
set -e
systemctl daemon-reload
POSTRM
    chmod 755 "$deb_root/DEBIAN/postinst" "$deb_root/DEBIAN/prerm" "$deb_root/DEBIAN/postrm"
    dpkg-deb --build "$deb_root" "$root/$output_dir/agent-up-ubuntu-$rid.deb"
    ;;
  nixos)
    pkgs_root="$stage/nixos-pkgs"
    mkdir -p "$pkgs_root/payload"
    cp -a "$stage/desktop" "$pkgs_root/payload/desktop"
    cp -a "$stage/server" "$pkgs_root/payload/server"
    cp -a "$stage/cli" "$pkgs_root/payload/cli"
    cat > "$pkgs_root/flake.nix" <<'NIX'
{
  description = "Agent-Up package set";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs }:
    let
      systems = [ "x86_64-linux" ];
      forAllSystems = nixpkgs.lib.genAttrs systems;
    in
    {
      packages = forAllSystems (system:
        let
          pkgs = import nixpkgs { inherit system; };
        in
        {
          agent-up = pkgs.stdenvNoCC.mkDerivation {
            pname = "agent-up";
            version = "@AGENT_UP_VERSION@";
            src = ./payload;
            installPhase = ''
              mkdir -p $out/opt/agent-up $out/bin
              cp -R desktop server cli $out/opt/agent-up/
              chmod +x $out/opt/agent-up/desktop/AgentUp.Desktop
              chmod +x $out/opt/agent-up/server/AgentUp.Server
              chmod +x $out/opt/agent-up/cli/AgentUp.CLI
              ln -s $out/opt/agent-up/desktop/AgentUp.Desktop $out/bin/agent-up-desktop
              ln -s $out/opt/agent-up/server/AgentUp.Server $out/bin/agent-up-server
              ln -s $out/opt/agent-up/cli/AgentUp.CLI $out/bin/agent-up
            '';
          };
          default = self.packages.${system}.agent-up;
        });
    };
}
NIX
    sed -i.bak "s|@AGENT_UP_VERSION@|${version#v}|g" "$pkgs_root/flake.nix"
    rm -f "$pkgs_root/flake.nix.bak"
    tar -C "$pkgs_root" -czf "$root/$output_dir/agent-up-nixos-pkgs.tar.gz" .
    ;;
  *)
    usage
    exit 2
    ;;
esac
