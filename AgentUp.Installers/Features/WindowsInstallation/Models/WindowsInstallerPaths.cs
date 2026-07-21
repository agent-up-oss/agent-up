namespace AgentUp.Installers.Features.WindowsInstallation.Models;

public sealed record WindowsInstallerPaths(
    string RootDirectory,
    string DesktopDirectory,
    string ServerDirectory,
    string CliDirectory,
    string BinDirectory,
    string StartMenuShortcutPath)
{
    public static WindowsInstallerPaths SystemDefault()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrWhiteSpace(programFiles))
            programFiles = @"C:\Program Files";

        var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
        if (string.IsNullOrWhiteSpace(commonStartMenu))
            commonStartMenu = @"C:\ProgramData\Microsoft\Windows\Start Menu";

        var root = WindowsCombine(programFiles, "Agent-Up");
        return new WindowsInstallerPaths(
            RootDirectory: root,
            DesktopDirectory: WindowsCombine(root, "desktop"),
            ServerDirectory: WindowsCombine(root, "server"),
            CliDirectory: WindowsCombine(root, "cli"),
            BinDirectory: WindowsCombine(root, "bin"),
            StartMenuShortcutPath: WindowsCombine(commonStartMenu, "Programs", "Agent-Up", "Agent-Up.lnk"));
    }

    public string UninstallScriptName { get; init; } = "uninstall-agent-up.ps1";

    public string DesktopExecutable => WindowsCombine(DesktopDirectory, "AgentUp.Desktop.exe");
    public string ServerExecutable => WindowsCombine(ServerDirectory, "AgentUp.Server.exe");
    public string CliExecutable => WindowsCombine(CliDirectory, "AgentUp.CLI.exe");
    public string CliShimPath => WindowsCombine(BinDirectory, WindowsInstallerManifest.DefaultCliShimName);
    public string UninstallScriptPath => WindowsCombine(RootDirectory, UninstallScriptName);

    private static string WindowsCombine(params string[] parts)
        => string.Join('\\', parts.Select(part => part.Trim('\\')).Where(part => !string.IsNullOrWhiteSpace(part)));
}
