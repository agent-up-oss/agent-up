using AgentUp.Installers.Features.WindowsInstallation.Models;

namespace AgentUp.Installers.Features.WindowsInstallation.Providers;

public static class WindowsTrayAutoStartProvider
{
    public static string RegisterPowerShell(WindowsInstallerManifest manifest, WindowsInstallerPaths paths)
        => $$"""
             $regPath = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'
             if (-not (Test-Path $regPath)) { New-Item -Force -Path $regPath | Out-Null }
             New-ItemProperty -Force -Path $regPath -Name '{{Ps(manifest.ProductName)}}' -Value '"{{Ps(paths.TrayExecutable)}}"' | Out-Null
             """;

    public static string CheckPowerShell(WindowsInstallerManifest manifest, WindowsInstallerPaths paths)
        => $$"""
           $expected = '"{{Ps(paths.TrayExecutable)}}"'
           $val = Get-ItemPropertyValue -Path 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run' -Name '{{Ps(manifest.ProductName)}}' -ErrorAction SilentlyContinue
           if (-not [string]::Equals($val, $expected, [System.StringComparison]::OrdinalIgnoreCase)) { exit 1 }
           exit 0
           """;

    public static string StopPowerShell(WindowsInstallerPaths paths)
        => $$"""
           $trayPath = '{{Ps(paths.TrayExecutable)}}'
           Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
             Where-Object { $_.ExecutablePath -and [string]::Equals($_.ExecutablePath, $trayPath, [System.StringComparison]::OrdinalIgnoreCase) } |
             ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
           """;

    public static string RemovePowerShell(WindowsInstallerManifest manifest)
        => $$"""
           $regPath = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Run'
           if (Test-Path $regPath) {
             Remove-ItemProperty -Path $regPath -Name '{{Ps(manifest.ProductName)}}' -ErrorAction SilentlyContinue
           }
           """;

    private static string Ps(string value) => value.Replace("'", "''");
}
