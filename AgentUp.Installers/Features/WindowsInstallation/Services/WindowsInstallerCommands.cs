using AgentUp.Installers.Features.WindowsInstallation.Models;

namespace AgentUp.Installers.Features.WindowsInstallation.Services;

public static class WindowsInstallerCommands
{
    public static IReadOnlyList<string> ServiceCreateArguments(WindowsInstallerManifest manifest, WindowsInstallerPaths paths)
        =>
        [
            "create",
            manifest.ServiceName,
            "binPath=",
            $"\"{paths.ServerExecutable}\" --urls {manifest.ServerUrl}",
            "start=",
            "auto",
            "DisplayName=",
            manifest.ServiceDisplayName
        ];

    public static IReadOnlyList<string> ServiceFailureArguments(WindowsInstallerManifest manifest)
        => ["failure", manifest.ServiceName, "reset=", "60", "actions=", "restart/5000/restart/5000/\"\"/5000"];

    public static string PrepareExistingServicePowerShell(WindowsInstallerManifest manifest)
        => $$"""
             $serviceName = '{{Ps(manifest.ServiceName)}}'
             $service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
             if ($null -ne $service) {
               if ($service.Status -ne 'Stopped') {
                 Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
                 $service.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
               }
               $service.Close()
               $service = $null
               sc.exe delete $serviceName | Out-Null
               $deleted = $false
               for ($attempt = 0; $attempt -lt 30; $attempt++) {
                 Start-Sleep -Milliseconds 500
                 sc.exe query $serviceName | Out-Null
                 if ($LASTEXITCODE -ne 0) {
                   $deleted = $true
                   break
                 }
               }
               if (-not $deleted) {
                 throw "Windows Service '$serviceName' is still registered after delete."
               }
             }
             exit 0
             """;

    public static string PathRemovePowerShell(string binDirectory)
        => $$"""
             $target = '{{Ps(binDirectory)}}'
             $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
             if (-not [string]::IsNullOrWhiteSpace($machinePath)) {
               $entries = $machinePath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_.TrimEnd('\') -ine $target.TrimEnd('\') }
               [Environment]::SetEnvironmentVariable('Path', ($entries -join ';'), 'Machine')
             }
             """;

    public static string PathUpdatePowerShell(string binDirectory)
        => $$"""
             $target = '{{Ps(binDirectory)}}'
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
             $shortcutPath = '{{Ps(paths.StartMenuShortcutPath)}}'
             New-Item -ItemType Directory -Force -Path (Split-Path -Parent $shortcutPath) | Out-Null
             $shell = New-Object -ComObject WScript.Shell
             $shortcut = $shell.CreateShortcut($shortcutPath)
             $shortcut.TargetPath = '{{Ps(paths.DesktopExecutable)}}'
             $shortcut.WorkingDirectory = '{{Ps(paths.DesktopDirectory)}}'
             $shortcut.IconLocation = '{{Ps(paths.DesktopExecutable)}},0'
             $shortcut.Save()
             """;

    public static string UninstallScript(WindowsInstallerManifest manifest, WindowsInstallerPaths paths)
        => $$"""
             $ErrorActionPreference = 'Stop'
             sc.exe stop {{manifest.ServiceName}} | Out-Null
             sc.exe delete {{manifest.ServiceName}} | Out-Null
             $machinePath = [Environment]::GetEnvironmentVariable('Path', 'Machine')
             if (-not [string]::IsNullOrWhiteSpace($machinePath)) {
               $target = '{{Ps(paths.BinDirectory)}}'
               $entries = $machinePath -split ';' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and $_.TrimEnd('\') -ine $target.TrimEnd('\') }
               [Environment]::SetEnvironmentVariable('Path', ($entries -join ';'), 'Machine')
             }
             Remove-Item -Force '{{Ps(paths.StartMenuShortcutPath)}}' -ErrorAction SilentlyContinue
             Remove-Item -Recurse -Force '{{Ps(paths.RootDirectory)}}' -ErrorAction SilentlyContinue
             Remove-Item -Recurse -Force 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{{Ps(manifest.RegistryKeyName)}}' -ErrorAction SilentlyContinue
             """;

    public static string UninstallRegistryPowerShell(WindowsInstallerManifest manifest, WindowsInstallerPaths paths)
        => $$"""
             $key = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{{Ps(manifest.RegistryKeyName)}}'
             New-Item -Force -Path $key | Out-Null
             New-ItemProperty -Force -Path $key -Name DisplayName -Value '{{Ps(manifest.ProductName)}}' | Out-Null
             New-ItemProperty -Force -Path $key -Name DisplayVersion -Value '{{Ps(manifest.Version)}}' | Out-Null
             New-ItemProperty -Force -Path $key -Name Publisher -Value '{{Ps(manifest.Manufacturer)}}' | Out-Null
             New-ItemProperty -Force -Path $key -Name InstallLocation -Value '{{Ps(paths.RootDirectory)}}' | Out-Null
             New-ItemProperty -Force -Path $key -Name DisplayIcon -Value '{{Ps(paths.DesktopExecutable)}},0' | Out-Null
             New-ItemProperty -Force -Path $key -Name NoModify -PropertyType DWord -Value 1 | Out-Null
             New-ItemProperty -Force -Path $key -Name NoRepair -PropertyType DWord -Value 1 | Out-Null
             New-ItemProperty -Force -Path $key -Name UninstallString -Value 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{{Ps(paths.UninstallScriptPath)}}"' | Out-Null
             New-ItemProperty -Force -Path $key -Name QuietUninstallString -Value 'powershell.exe -NoProfile -ExecutionPolicy Bypass -File "{{Ps(paths.UninstallScriptPath)}}"' | Out-Null
             """;

    public static string FreshShellCliLookupPowerShell(string cliCommandName)
        => $"Get-Command {cliCommandName} -ErrorAction Stop | Out-Null";

    private static string Ps(string value) => value.Replace("'", "''");
}
