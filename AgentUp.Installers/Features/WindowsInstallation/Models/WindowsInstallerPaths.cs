namespace AgentUp.Installers.Features.WindowsInstallation.Models;

public sealed partial record WindowsInstallerPaths(
    string RootDirectory,
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory,
    string TrayDirectory,
    string BinDirectory,
    string StartMenuShortcutPath)
{
    public string UninstallScriptName { get; init; } = $"uninstall-{WindowsInstallerManifest.DefaultCliShimName[..^4]}.ps1";

    public string DesktopExecutable => WindowsCombine(DesktopDirectory, "AgentUp.Desktop.exe");
    public string ServerExecutable => WindowsCombine(ServerDirectory, "AgentUp.Server.exe");
    public string TrayExecutable => WindowsCombine(TrayDirectory, "AgentUp.Tray.exe");
    public string CliExecutable => WindowsCombine(CliDirectory, "AgentUp.CLI.exe");
    public string CliShimPath => CliShimPathFor(WindowsInstallerManifest.DefaultCliShimName);
    public string CliShimPathFor(string cliShimName)
        => WindowsCombine(BinDirectory, WindowsInstallerManifest.RequireSafeCliShimFileName(cliShimName));
    public string UninstallScriptPath => WindowsCombine(RootDirectory, UninstallScriptName);

    public static string WindowsCombine(params string[] parts)
        => string.Join('\\', parts.Select(part => part.Trim('\\')).Where(part => !string.IsNullOrWhiteSpace(part)));
}
