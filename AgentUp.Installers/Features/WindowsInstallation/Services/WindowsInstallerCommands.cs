using AgentUp.Installers.Features.WindowsInstallation.Models;

namespace AgentUp.Installers.Features.WindowsInstallation.Services;

public static class WindowsInstallerCommands
{
    public static string ServiceCreateArguments(WindowsInstallerManifest manifest, WindowsInstallerPaths paths)
        => $"create {manifest.ServiceName} binPath= \"\\\"{paths.ServerExecutable}\\\" --urls {manifest.ServerUrl}\" start= auto DisplayName= \"Agent-Up Server\"";

    public static string ServiceFailureArguments(WindowsInstallerManifest manifest)
        => $"failure {manifest.ServiceName} reset= 60 actions= restart/5000/restart/5000/\"\"/5000";

    public static string PathUpdatePowerShell(string binDirectory)
        => $$"""
             $target = '{{binDirectory}}'
             $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
             $entries = @()
             if (-not [string]::IsNullOrWhiteSpace($machinePath)) {
               $entries = $machinePath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
             }
             if (-not ($entries | Where-Object { $_.TrimEnd('\') -ieq $target.TrimEnd('\') })) {
               [Environment]::SetEnvironmentVariable('Path', (($entries + $target) -join ';'), 'Machine')
             }
             """;

    public static string ShortcutPowerShell(WindowsInstallerPaths paths)
        => $$"""
             $shortcutPath = '{{paths.StartMenuShortcutPath}}'
             New-Item -ItemType Directory -Force -Path (Split-Path -Parent $shortcutPath) | Out-Null
             $shell = New-Object -ComObject WScript.Shell
             $shortcut = $shell.CreateShortcut($shortcutPath)
             $shortcut.TargetPath = '{{paths.DesktopExecutable}}'
             $shortcut.WorkingDirectory = '{{paths.DesktopDirectory}}'
             $shortcut.IconLocation = '{{paths.DesktopExecutable}},0'
             $shortcut.Save()
             """;

    public static string UninstallScript(WindowsInstallerManifest manifest, WindowsInstallerPaths paths)
        => $$"""
             $ErrorActionPreference = 'Stop'
             sc.exe stop {{manifest.ServiceName}} | Out-Null
             sc.exe delete {{manifest.ServiceName}} | Out-Null
             $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
             if (-not [string]::IsNullOrWhiteSpace($machinePath)) {
               $target = '{{paths.BinDirectory}}'
               $entries = $machinePath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_.TrimEnd('\') -ine $target.TrimEnd('\') }
               [Environment]::SetEnvironmentVariable('Path', ($entries -join ';'), 'Machine')
             }
             Remove-Item -Force '{{paths.StartMenuShortcutPath}}' -ErrorAction SilentlyContinue
             Remove-Item -Recurse -Force '{{paths.RootDirectory}}' -ErrorAction SilentlyContinue
             Remove-Item -Recurse -Force 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Agent-Up' -ErrorAction SilentlyContinue
             """;

    public static string UninstallRegistryPowerShell(WindowsInstallerManifest manifest, WindowsInstallerPaths paths)
        => $$"""
             $key = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Agent-Up'
             New-Item -Force -Path $key | Out-Null
             New-ItemProperty -Force -Path $key -Name DisplayName -Value '{{manifest.ProductName}}' | Out-Null
             New-ItemProperty -Force -Path $key -Name DisplayVersion -Value '{{manifest.Version}}' | Out-Null
             New-ItemProperty -Force -Path $key -Name Publisher -Value '{{manifest.Manufacturer}}' | Out-Null
             New-ItemProperty -Force -Path $key -Name InstallLocation -Value '{{paths.RootDirectory}}' | Out-Null
             New-ItemProperty -Force -Path $key -Name UninstallString -Value 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{{paths.UninstallScriptPath}}"' | Out-Null
             New-ItemProperty -Force -Path $key -Name QuietUninstallString -Value 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{{paths.UninstallScriptPath}}"' | Out-Null
             """;

    public static string FreshShellCliLookupPowerShell()
        => "Get-Command agent-up -ErrorAction Stop | Out-Null";
}
