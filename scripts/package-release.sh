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
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
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
using System.Security.Principal;
using Microsoft.Win32;
using System.Windows.Forms;

var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Agent-Up");
var extractOnly = false;
var quiet = false;
var requestedAction = "";

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
        case "--install":
            requestedAction = "install";
            break;
        case "--upgrade":
            requestedAction = "upgrade";
            break;
        case "--uninstall":
            requestedAction = "uninstall";
            break;
        default:
            throw new ArgumentException($"Unknown argument: {args[i]}");
    }
}

installDir = Path.GetFullPath(installDir);

if (extractOnly)
{
    ExtractPayload(installDir);
    return 0;
}

if (!IsAdministrator())
{
    RelaunchElevated(args);
    return 0;
}

var existing = IsInstalled(installDir);
var action = requestedAction;
if (string.IsNullOrWhiteSpace(action))
    action = quiet ? (existing ? "upgrade" : "install") : ChooseAction(existing);

if (action == "cancel")
    return 0;

try
{
    switch (action)
    {
        case "install":
            await InstallAsync(installDir, uninstallFirst: false);
            break;
        case "upgrade":
            await InstallAsync(installDir, uninstallFirst: true);
            break;
        case "uninstall":
            await UninstallAsync(installDir);
            break;
        default:
            throw new InvalidOperationException($"Unknown installer action: {action}");
    }

    if (!quiet)
        MessageBox.Show($"Agent-Up {action} completed.", "Agent-Up Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);

    return 0;
}
catch (Exception ex)
{
    if (!quiet)
        MessageBox.Show(ex.Message, "Agent-Up Installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
    Console.Error.WriteLine(ex);
    return 1;
}

async Task InstallAsync(string targetDir, bool uninstallFirst)
{
    if (uninstallFirst)
        await UninstallAsync(targetDir);

    Directory.CreateDirectory(targetDir);
    ExtractPayload(targetDir);
    CopySelfToInstallDir(targetDir);
    await RunPowerShellAsync(Path.Combine(targetDir, "tools", "install-agent-up-server.ps1"));
    CreateCliShim(targetDir);
    AddToMachinePath(Path.Combine(targetDir, "bin"));
    CreateStartMenuShortcut(targetDir);
    RegisterApp(targetDir);
}

async Task UninstallAsync(string targetDir)
{
    var uninstallScript = Path.Combine(targetDir, "tools", "uninstall-agent-up-server.ps1");
    if (File.Exists(uninstallScript))
        await RunPowerShellAsync(uninstallScript);

    RemoveFromMachinePath(Path.Combine(targetDir, "bin"));
    RemoveStartMenuShortcut();
    UnregisterApp();

    if (Directory.Exists(targetDir) && IsCurrentExecutableUnder(targetDir))
    {
        ScheduleDirectoryDelete(targetDir);
    }
    else if (Directory.Exists(targetDir))
    {
        Directory.Delete(targetDir, recursive: true);
    }
}

void ExtractPayload(string targetDir)
{
    Directory.CreateDirectory(targetDir);
    using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
        ?? throw new InvalidOperationException("Embedded payload.zip was not found.");
    using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
    archive.ExtractToDirectory(targetDir, overwriteFiles: true);
}

void CopySelfToInstallDir(string targetDir)
{
    var currentExe = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(currentExe) || !File.Exists(currentExe))
        return;

    File.Copy(currentExe, Path.Combine(targetDir, "AgentUp.Installer.exe"), overwrite: true);
}

async Task RunPowerShellAsync(string script)
{
    if (!File.Exists(script))
        throw new FileNotFoundException($"Installer script not found: {script}", script);

    var startInfo = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add("-NoProfile");
    startInfo.ArgumentList.Add("-ExecutionPolicy");
    startInfo.ArgumentList.Add("Bypass");
    startInfo.ArgumentList.Add("-File");
    startInfo.ArgumentList.Add(script);

    using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start PowerShell.");
    await process.WaitForExitAsync();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"{Path.GetFileName(script)} exited with code {process.ExitCode}.");
}

void CreateCliShim(string targetDir)
{
    var binDir = Path.Combine(targetDir, "bin");
    Directory.CreateDirectory(binDir);
    File.WriteAllText(Path.Combine(binDir, "agent-up.cmd"),
        "@echo off\r\n\"%~dp0..\\cli\\AgentUp.CLI.exe\" %*\r\n");
}

void CreateStartMenuShortcut(string targetDir)
{
    var programs = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
    Directory.CreateDirectory(programs);
    var shortcut = Path.Combine(programs, "Agent-Up.lnk");
    var shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
    var link = shell!.GetType().InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, [shortcut])!;
    link.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, link, [Path.Combine(targetDir, "desktop", "AgentUp.Desktop.exe")]);
    link.GetType().InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, link, [Path.Combine(targetDir, "desktop")]);
    link.GetType().InvokeMember("IconLocation", System.Reflection.BindingFlags.SetProperty, null, link, [Path.Combine(targetDir, "desktop", "AgentUp.Desktop.exe")]);
    link.GetType().InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, link, null);
}

void RemoveStartMenuShortcut()
{
    var shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Agent-Up.lnk");
    if (File.Exists(shortcut))
        File.Delete(shortcut);
}

void RegisterApp(string targetDir)
{
    using var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Agent-Up");
    key.SetValue("DisplayName", "Agent-Up");
    key.SetValue("DisplayVersion", "@AGENT_UP_VERSION@");
    key.SetValue("Publisher", "Agent-Up");
    key.SetValue("InstallLocation", targetDir);
    key.SetValue("DisplayIcon", Path.Combine(targetDir, "desktop", "AgentUp.Desktop.exe"));
    key.SetValue("UninstallString", $"\"{Path.Combine(targetDir, "AgentUp.Installer.exe")}\" --uninstall");
    key.SetValue("QuietUninstallString", $"\"{Path.Combine(targetDir, "AgentUp.Installer.exe")}\" --uninstall --quiet");
    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
}

void UnregisterApp()
{
    Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Agent-Up", throwOnMissingSubKey: false);
}

bool IsCurrentExecutableUnder(string targetDir)
{
    var currentExe = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(currentExe))
        return false;

    var current = Path.GetFullPath(currentExe).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    var target = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    return current.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}

void ScheduleDirectoryDelete(string targetDir)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "cmd.exe",
        UseShellExecute = false,
        CreateNoWindow = true
    };
    startInfo.ArgumentList.Add("/c");
    startInfo.ArgumentList.Add($"ping 127.0.0.1 -n 3 > nul & rmdir /s /q \"{targetDir}\"");
    Process.Start(startInfo);
}

void AddToMachinePath(string path)
{
    using var env = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", writable: true)
        ?? throw new InvalidOperationException("Machine environment registry key was not found.");
    var current = (env.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string) ?? "";
    var parts = current.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
        return;

    env.SetValue("Path", string.IsNullOrWhiteSpace(current) ? path : current.TrimEnd(';') + ";" + path, RegistryValueKind.ExpandString);
    BroadcastEnvironmentChange();
}

void RemoveFromMachinePath(string path)
{
    using var env = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", writable: true);
    if (env is null) return;

    var current = (env.GetValue("Path", "", RegistryValueOptions.DoNotExpandEnvironmentNames) as string) ?? "";
    var updated = string.Join(";", current.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)));
    env.SetValue("Path", updated, RegistryValueKind.ExpandString);
    BroadcastEnvironmentChange();
}

void BroadcastEnvironmentChange()
{
    const int HWND_BROADCAST = 0xffff;
    const int WM_SETTINGCHANGE = 0x001A;
    SendMessageTimeout((IntPtr)HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero, "Environment", 0, 5000, out _);
}

bool IsInstalled(string targetDir) =>
    Directory.Exists(targetDir)
    || Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Agent-Up") is not null;

bool IsAdministrator()
{
    using var identity = WindowsIdentity.GetCurrent();
    return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
}

void RelaunchElevated(string[] originalArgs)
{
    var exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot determine installer executable path.");
    var startInfo = new ProcessStartInfo
    {
        FileName = exe,
        UseShellExecute = true,
        Verb = "runas"
    };
    foreach (var arg in originalArgs)
        startInfo.ArgumentList.Add(arg);
    Process.Start(startInfo);
}

string ChooseAction(bool existingInstall)
{
    Application.EnableVisualStyles();
    using var form = new Form
    {
        Text = "Agent-Up Installer",
        Width = 360,
        Height = existingInstall ? 180 : 140,
        StartPosition = FormStartPosition.CenterScreen,
        FormBorderStyle = FormBorderStyle.FixedDialog,
        MaximizeBox = false,
        MinimizeBox = false
    };

    var label = new Label
    {
        Text = existingInstall
            ? "Agent-Up is already installed. Choose an action."
            : "Install Agent-Up Desktop, CLI, and Server service.",
        Left = 20,
        Top = 20,
        Width = 300,
        Height = 40
    };
    form.Controls.Add(label);

    var result = "cancel";
    var left = 20;
    void AddButton(string text, string action)
    {
        var button = new Button { Text = text, Left = left, Top = 80, Width = 95, Height = 32 };
        left += 105;
        button.Click += (_, _) => { result = action; form.Close(); };
        form.Controls.Add(button);
    }

    if (existingInstall)
    {
        AddButton("Upgrade", "upgrade");
        AddButton("Uninstall", "uninstall");
    }
    else
    {
        AddButton("Install", "install");
    }
    AddButton("Cancel", "cancel");

    form.ShowDialog();
    return result;
}

[System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
static extern IntPtr SendMessageTimeout(
    IntPtr hWnd,
    int Msg,
    IntPtr wParam,
    string lParam,
    int fuFlags,
    int uTimeout,
    out IntPtr lpdwResult);
CS

  sed -i.bak "s|@AGENT_UP_VERSION@|${version#v}|g" "$installer_src/Program.cs"
  rm -f "$installer_src/Program.cs.bak"

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
action="${1:-install}"

uninstall_agent_up() {
  launchctl bootout system /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true
  rm -f /Library/LaunchDaemons/dev.agent-up.server.plist
  rm -f /usr/local/bin/agent-up /usr/local/bin/agent-up-server /usr/local/bin/agent-up-desktop
  rm -rf /usr/local/agent-up
  rm -rf "/Library/Application Support/Agent-Up"
  rm -rf /Applications/Agent-Up.app
}

install_agent_up() {
  rm -rf /Applications/Agent-Up.app
  cp -R "$root/Agent-Up.app" /Applications/Agent-Up.app
  chmod +x /Applications/Agent-Up.app/Contents/MacOS/AgentUp.Desktop
  chmod +x /Applications/Agent-Up.app/Contents/Resources/server/AgentUp.Server

  mkdir -p "/Library/Application Support/Agent-Up"
  mkdir -p "/Library/Logs/Agent-Up"
  mkdir -p /usr/local/agent-up/cli /usr/local/bin
  rm -rf /usr/local/agent-up/cli
  cp -R "$root/cli" /usr/local/agent-up/cli
  chmod +x /usr/local/agent-up/cli/AgentUp.CLI
  ln -sf /usr/local/agent-up/cli/AgentUp.CLI /usr/local/bin/agent-up
  ln -sf /Applications/Agent-Up.app/Contents/Resources/server/AgentUp.Server /usr/local/bin/agent-up-server
  ln -sf /Applications/Agent-Up.app/Contents/MacOS/AgentUp.Desktop /usr/local/bin/agent-up-desktop

  cp "$root/agent-up-server.plist" /Library/LaunchDaemons/dev.agent-up.server.plist
  chown root:wheel /Library/LaunchDaemons/dev.agent-up.server.plist
  chmod 644 /Library/LaunchDaemons/dev.agent-up.server.plist
  launchctl bootout system /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true
  launchctl bootstrap system /Library/LaunchDaemons/dev.agent-up.server.plist
  launchctl kickstart -k system/dev.agent-up.server
}

case "$action" in
  install)
    install_agent_up
    echo "Agent-Up installed. Start Desktop from /Applications/Agent-Up.app"
    ;;
  upgrade)
    uninstall_agent_up
    install_agent_up
    echo "Agent-Up upgraded. Start Desktop from /Applications/Agent-Up.app"
    ;;
  uninstall)
    uninstall_agent_up
    echo "Agent-Up uninstalled."
    ;;
  *)
    echo "Usage: $0 [install|upgrade|uninstall]" >&2
    exit 2
    ;;
esac
INSTALL
    chmod +x "$stage/install.sh"
    cat > "$stage/uninstall.sh" <<'UNINSTALL'
#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ "$(id -u)" -eq 0 ]; then
  "$root/install.sh" uninstall
else
  sudo "$root/install.sh" uninstall
fi
UNINSTALL
    chmod +x "$stage/uninstall.sh"
    installer_app="$stage/Agent-Up Installer.app"
    mkdir -p "$installer_app/Contents/MacOS" "$installer_app/Contents/Resources"
    cat > "$installer_app/Contents/Info.plist" <<'MACPLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key>
  <string>dev.agent-up.installer</string>
  <key>CFBundleName</key>
  <string>Agent-Up Installer</string>
  <key>CFBundleDisplayName</key>
  <string>Agent-Up Installer</string>
  <key>CFBundleExecutable</key>
  <string>AgentUpInstaller</string>
  <key>CFBundleVersion</key>
  <string>1</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
</dict>
</plist>
MACPLIST
    cat > "$installer_app/Contents/MacOS/AgentUpInstaller" <<'MACGUI'
#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"

if [ -d /Applications/Agent-Up.app ] || [ -f /Library/LaunchDaemons/dev.agent-up.server.plist ]; then
  action="$(osascript -e 'button returned of (display dialog "Agent-Up is already installed. Choose an action." buttons {"Cancel", "Uninstall", "Upgrade"} default button "Upgrade" cancel button "Cancel" with title "Agent-Up Installer")' || true)"
else
  action="$(osascript -e 'button returned of (display dialog "Install Agent-Up Desktop, CLI, and Server service?" buttons {"Cancel", "Install"} default button "Install" cancel button "Cancel" with title "Agent-Up Installer")' || true)"
fi

case "$action" in
  Install|Upgrade|Uninstall)
    lower="$(printf "%s" "$action" | tr '[:upper:]' '[:lower:]')"
    osascript -e "do shell script quoted form of \"$root/install.sh\" & \" $lower\" with administrator privileges"
    osascript -e "display dialog \"Agent-Up $lower completed.\" buttons {\"OK\"} default button \"OK\" with title \"Agent-Up Installer\""
    ;;
  *)
    exit 0
    ;;
esac
MACGUI
    chmod +x "$installer_app/Contents/MacOS/AgentUpInstaller"
    dmg_root="$stage/dmg-root"
    mkdir -p "$dmg_root"
    cp -R "$stage/Agent-Up.app" "$dmg_root/"
    cp -R "$stage/cli" "$dmg_root/"
    cp "$stage/agent-up-server.plist" "$dmg_root/"
    cp "$stage/install.sh" "$dmg_root/"
    cp "$stage/uninstall.sh" "$dmg_root/"
    cp -R "$installer_app" "$dmg_root/"
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
    mkdir -p "$deb_root/DEBIAN" "$deb_root/opt/agent-up" "$deb_root/etc/systemd/system" "$deb_root/usr/bin" "$deb_root/usr/share/applications" "$deb_root/usr/share/pixmaps"
    cp -a "$stage/desktop" "$deb_root/opt/agent-up/desktop"
    cp -a "$stage/server" "$deb_root/opt/agent-up/server"
    cp -a "$stage/cli" "$deb_root/opt/agent-up/cli"
    cp "$root/media/logo.png" "$deb_root/usr/share/pixmaps/agent-up.png"
    cp "$stage/share/agent-up-server.service" "$deb_root/etc/systemd/system/agent-up-server.service"
    chmod +x "$deb_root/opt/agent-up/desktop/AgentUp.Desktop" "$deb_root/opt/agent-up/server/AgentUp.Server" "$deb_root/opt/agent-up/cli/AgentUp.CLI"
    ln -s /opt/agent-up/cli/AgentUp.CLI "$deb_root/usr/bin/agent-up"
    deb_version="${version#v}"
    cat > "$deb_root/usr/share/applications/agent-up.desktop" <<DESKTOP
[Desktop Entry]
Type=Application
Name=Agent-Up
Comment=Agent-Up desktop workspace client
Exec=/opt/agent-up/desktop/AgentUp.Desktop
Icon=agent-up
Terminal=false
Categories=Development;
StartupNotify=true
X-AgentUp-Version=$deb_version
DESKTOP
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
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database /usr/share/applications || true
fi
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
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database /usr/share/applications || true
fi
POSTRM
    chmod 755 "$deb_root/DEBIAN/postinst" "$deb_root/DEBIAN/prerm" "$deb_root/DEBIAN/postrm"
    dpkg-deb --build "$deb_root" "$root/$output_dir/agent-up-ubuntu-$rid.deb"
    ;;
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
