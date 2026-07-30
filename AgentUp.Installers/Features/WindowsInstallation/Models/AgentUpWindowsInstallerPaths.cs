namespace AgentUp.Installers.Features.WindowsInstallation.Models;

public sealed partial record WindowsInstallerPaths
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
            TrayDirectory: WindowsCombine(root, "tray"),
            BinDirectory: WindowsCombine(root, "bin"),
            StartMenuShortcutPath: WindowsCombine(commonStartMenu, "Programs", "Agent-Up", "Agent-Up.lnk"),
            CliShimName: WindowsInstallerManifest.DefaultCliShimName,
            UninstallScriptName: "uninstall-agent-up.ps1");
    }
}
