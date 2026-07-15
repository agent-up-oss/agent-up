namespace AgentUp.Installers.Features.MacOs;

public sealed record MacOsInstallerPaths(
    string AppBundleDirectory,
    string ApplicationSupportDirectory,
    string ServerDirectory,
    string CliDirectory,
    string LaunchDaemonPath,
    string LogsDirectory,
    string CliSymlinkPath,
    string ServerSymlinkPath,
    string DesktopSymlinkPath)
{
    public static MacOsInstallerPaths SystemDefault()
        => new(
            AppBundleDirectory: "/Applications/Agent-Up.app",
            ApplicationSupportDirectory: "/Library/Application Support/Agent-Up",
            ServerDirectory: "/Library/Application Support/Agent-Up/server",
            CliDirectory: "/usr/local/agent-up/cli",
            LaunchDaemonPath: "/Library/LaunchDaemons/dev.agent-up.server.plist",
            LogsDirectory: "/Library/Logs/Agent-Up",
            CliSymlinkPath: "/usr/local/bin/agent-up",
            ServerSymlinkPath: "/usr/local/bin/agent-up-server",
            DesktopSymlinkPath: "/usr/local/bin/agent-up-desktop");

    public string DesktopExecutable => System.IO.Path.Combine(AppBundleDirectory, "Contents", "MacOS", "AgentUp.Desktop");
    public string ServerExecutable => System.IO.Path.Combine(ServerDirectory, "AgentUp.Server");
    public string CliExecutable => System.IO.Path.Combine(CliDirectory, "AgentUp.CLI");
    public string DesktopInfoPlistPath => System.IO.Path.Combine(AppBundleDirectory, "Contents", "Info.plist");
}
