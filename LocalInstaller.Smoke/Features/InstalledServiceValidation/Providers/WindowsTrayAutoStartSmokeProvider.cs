using LocalInstaller.Smoke.Features.InstalledServiceValidation.Models;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;

namespace LocalInstaller.Smoke.Features.InstalledServiceValidation.Providers;

public sealed class WindowsTrayAutoStartSmokeProvider
{
    private const string TrayAutoStartCheck = "$name = $env:AGENTUP_TRAY_AUTOSTART_NAME; $expected = $env:AGENTUP_TRAY_AUTOSTART_VALUE; $val = Get-ItemPropertyValue -Path 'HKLM:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name $name -ErrorAction SilentlyContinue; if (-not [string]::Equals($val, $expected, [System.StringComparison]::OrdinalIgnoreCase)) { throw \"$name tray autostart registry entry missing or incorrect\" }";

    public CommandSpec ValidationCommand(SmokeProductConfig product, string trayExecutable)
        => new("powershell.exe", ["-NoProfile", "-Command", TrayAutoStartCheck],
            Environment: new Dictionary<string, string>
            {
                ["AGENTUP_TRAY_AUTOSTART_NAME"] = product.DisplayName,
                ["AGENTUP_TRAY_AUTOSTART_VALUE"] = $"\"{trayExecutable}\""
            });
}
