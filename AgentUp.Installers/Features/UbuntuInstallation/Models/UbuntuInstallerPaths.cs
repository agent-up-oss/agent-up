namespace AgentUp.Installers.Features.UbuntuInstallation.Models;

public sealed record UbuntuInstallerPaths(
    string RootDirectory,
    string ServicePath,
    string CliSymlinkPath,
    string DesktopEntryPath,
    string IconPath,
    string DataDirectory,
    string LogPath,
    string ErrorLogPath)
{
    public static UbuntuInstallerPaths SystemDefault()
        => ForProduct(UbuntuInstallerManifest.AgentUp());

    public static UbuntuInstallerPaths ForProduct(UbuntuInstallerManifest manifest)
        => new(
            RootDirectory: $"/opt/{manifest.PackageName}",
            ServicePath: $"/etc/systemd/system/{manifest.ServiceUnitName}",
            CliSymlinkPath: $"/usr/bin/{manifest.CliCommandName}",
            DesktopEntryPath: $"/usr/share/applications/{manifest.PackageName}.desktop",
            IconPath: $"/usr/share/pixmaps/{manifest.PackageName}.png",
            DataDirectory: $"/var/lib/{manifest.PackageName}",
            LogPath: $"/var/log/{manifest.PackageName}-server.log",
            ErrorLogPath: $"/var/log/{manifest.PackageName}-server.err.log");

    public string DesktopDirectory => System.IO.Path.Join(RootDirectory, "desktop");
    public string ServerDirectory => System.IO.Path.Join(RootDirectory, "server");
    public string CliDirectory => System.IO.Path.Join(RootDirectory, "cli");
    public string TrayDirectory => System.IO.Path.Join(RootDirectory, "tray");
    public string DesktopExecutable => System.IO.Path.Join(DesktopDirectory, "AgentUp.Desktop");
    public string ServerExecutable => System.IO.Path.Join(ServerDirectory, "AgentUp.Server");
    public string CliExecutable => System.IO.Path.Join(CliDirectory, "AgentUp.CLI");
    public string TrayExecutable => System.IO.Path.Join(TrayDirectory, "AgentUp.Tray");
    public string XdgAutostartPath => System.IO.Path.Join("/etc/xdg/autostart", $"{PackageName(DesktopEntryPath)}-tray.desktop");

    private static string PackageName(string desktopEntryPath)
        => System.IO.Path.GetFileNameWithoutExtension(desktopEntryPath);
}
