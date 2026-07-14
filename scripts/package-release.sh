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

  if command -v zip >/dev/null 2>&1; then
    zip -qr "$output" "$@"
  elif tar --help 2>/dev/null | grep -q -- '-a'; then
    tar -a -cf "$output" "$@"
  else
    echo "zip or tar -a is required to create $output" >&2
    exit 1
  fi
}

rm -rf "$stage"
mkdir -p "$stage/desktop" "$stage/server" "$stage/cli" "$root/$output_dir"

dotnet publish "$root/AgentUp.Desktop/AgentUp.Desktop.csproj" \
  --configuration "$configuration" \
  --runtime "$rid" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:Version="$version" \
  -o "$stage/desktop"

dotnet publish "$root/AgentUp.Server/AgentUp.Server.csproj" \
  --configuration "$configuration" \
  --runtime "$rid" \
  --self-contained true \
  -p:PublishSingleFile=false \
  -p:Version="$version" \
  -o "$stage/server"

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
sudo cp "$root/agent-up-server.plist" /Library/LaunchDaemons/dev.agent-up.server.plist
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
sudo rm -rf /Applications/Agent-Up.app
UNINSTALL
    chmod +x "$stage/uninstall.sh"
    (cd "$stage" && create_zip "$root/$output_dir/agent-up-macos-$rid.zip" "Agent-Up.app" cli agent-up-server.plist install.sh uninstall.sh)
    ;;
  windows)
    mkdir -p "$stage/tools"
    cp "$root/packaging/windows/install-agent-up-server.ps1" "$stage/tools/"
    cp "$root/packaging/windows/uninstall-agent-up-server.ps1" "$stage/tools/"
    (cd "$stage" && create_zip "$root/$output_dir/agent-up-windows-$rid.zip" desktop server cli tools)
    ;;
  ubuntu|nixos)
    mkdir -p "$stage/share"
    cp "$root/packaging/linux/agent-up-server.service" "$stage/share/"
    cat > "$stage/install.sh" <<'INSTALL'
#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
sudo mkdir -p /opt/agent-up
sudo rm -rf /opt/agent-up/desktop /opt/agent-up/server /opt/agent-up/cli
sudo cp -a "$root/desktop" /opt/agent-up/desktop
sudo cp -a "$root/server" /opt/agent-up/server
sudo cp -a "$root/cli" /opt/agent-up/cli
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
    if [ "$platform" = "nixos" ]; then
      cat > "$stage/share/agent-up-nixos-module.nix" <<'NIX'
{ config, lib, pkgs, ... }:

{
  systemd.services.agent-up-server = {
    description = "Agent-Up Server";
    wantedBy = [ "multi-user.target" ];
    after = [ "network.target" ];
    serviceConfig = {
      ExecStart = "/opt/agent-up/server/AgentUp.Server";
      Restart = "on-failure";
      RestartSec = 5;
      Environment = "ASPNETCORE_URLS=http://localhost:5000";
    };
  };
}
NIX
    fi
    tar -C "$stage" -czf "$root/$output_dir/agent-up-$platform-$rid.tar.gz" desktop server cli share install.sh uninstall.sh
    ;;
  *)
    usage
    exit 2
    ;;
esac
