namespace AgentUp.Installers.Features.MacOsInstallation.Models;

public sealed partial record MacOsInstallerPaths
{
    public static MacOsInstallerPaths SystemDefault()
        => new(
            AppBundleDirectory: "/Applications/Agent-Up.app",
            ApplicationSupportDirectory: "/Library/Application Support/Agent-Up",
            ServerDirectory: "/Library/Application Support/Agent-Up/server",
            TrayDirectory: "/Library/Application Support/Agent-Up/tray",
            CliDirectory: "/usr/local/agent-up/cli",
            LaunchDaemonPath: "/Library/LaunchDaemons/dev.agent-up.server.plist",
            LogsDirectory: "/Library/Logs/Agent-Up",
            CliSymlinkPath: "/usr/local/bin/agent-up",
            ServerSymlinkPath: "/usr/local/bin/agent-up-server",
            DesktopSymlinkPath: "/usr/local/bin/agent-up-desktop",
            BundleIconFile: "Agent-Up.png");
}
