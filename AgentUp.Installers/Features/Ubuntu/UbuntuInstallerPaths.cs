namespace AgentUp.Installers.Features.Ubuntu;

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
        => new(
            RootDirectory: "/opt/agent-up",
            ServicePath: "/etc/systemd/system/agent-up-server.service",
            CliSymlinkPath: "/usr/bin/agent-up",
            DesktopEntryPath: "/usr/share/applications/agent-up.desktop",
            IconPath: "/usr/share/pixmaps/agent-up.png",
            DataDirectory: "/var/lib/agent-up",
            LogPath: "/var/log/agent-up-server.log",
            ErrorLogPath: "/var/log/agent-up-server.err.log");

    public string DesktopDirectory => System.IO.Path.Combine(RootDirectory, "desktop");
    public string ServerDirectory => System.IO.Path.Combine(RootDirectory, "server");
    public string CliDirectory => System.IO.Path.Combine(RootDirectory, "cli");
    public string DesktopExecutable => System.IO.Path.Combine(DesktopDirectory, "AgentUp.Desktop");
    public string ServerExecutable => System.IO.Path.Combine(ServerDirectory, "AgentUp.Server");
    public string CliExecutable => System.IO.Path.Combine(CliDirectory, "AgentUp.CLI");
}
