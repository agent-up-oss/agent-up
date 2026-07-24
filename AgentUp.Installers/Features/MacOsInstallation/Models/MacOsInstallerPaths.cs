using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.MacOsInstallation.Models;

public sealed record MacOsInstallerPaths(
    string AppBundleDirectory,
    string ApplicationSupportDirectory,
    string ServerDirectory,
    string TrayDirectory,
    string CliDirectory,
    string LaunchDaemonPath,
    string LogsDirectory,
    string CliSymlinkPath,
    string ServerSymlinkPath,
    string DesktopSymlinkPath,
    string BundleIconFile)
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

    public static MacOsInstallerPaths From(ProductManifest product)
    {
        var identity = MacOsInstallerManifest.ValidatedIdentityFrom(product);
        var appBundleDirectory = Under("/Applications", $"{identity.ProductName}.app");
        var applicationSupportDirectory = Under("/Library/Application Support", identity.ProductName);
        var serverDirectory = Under(applicationSupportDirectory, "server");
        var trayDirectory = Under(applicationSupportDirectory, "tray");
        var cliDirectory = Under("/usr/local", identity.Slug, "cli");
        var launchDaemonPath = Under("/Library/LaunchDaemons", $"dev.{identity.Slug}.server.plist");
        var logsDirectory = Under("/Library/Logs", identity.ProductName);
        var cliSymlinkPath = Under("/usr/local/bin", identity.Slug);
        var serverSymlinkPath = Under("/usr/local/bin", $"{identity.Slug}-server");
        var desktopSymlinkPath = Under("/usr/local/bin", $"{identity.Slug}-desktop");

        return new(
            AppBundleDirectory: appBundleDirectory,
            ApplicationSupportDirectory: applicationSupportDirectory,
            ServerDirectory: serverDirectory,
            TrayDirectory: trayDirectory,
            CliDirectory: cliDirectory,
            LaunchDaemonPath: launchDaemonPath,
            LogsDirectory: logsDirectory,
            CliSymlinkPath: cliSymlinkPath,
            ServerSymlinkPath: serverSymlinkPath,
            DesktopSymlinkPath: desktopSymlinkPath,
            BundleIconFile: identity.BundleIconFile);
    }

    private static string Under(string root, params string[] segments)
    {
        var path = System.IO.Path.GetFullPath(System.IO.Path.Join([root, .. segments]));
        var normalizedRoot = System.IO.Path.GetFullPath(root).TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;

        if (!path.StartsWith(normalizedRoot, StringComparison.Ordinal))
            throw new ArgumentException($"Resolved macOS installer path '{path}' must remain under '{root}'.");

        return path;
    }

    public string DesktopExecutable => System.IO.Path.Join(AppBundleDirectory, "Contents", "MacOS", "AgentUp.Desktop");
    public string ServerExecutable => System.IO.Path.Join(ServerDirectory, "AgentUp.Server");
    public string TrayExecutable => System.IO.Path.Join(TrayDirectory, "AgentUp.Tray");
    public string CliExecutable => System.IO.Path.Join(CliDirectory, "AgentUp.CLI");
    public string DesktopInfoPlistPath => System.IO.Path.Join(AppBundleDirectory, "Contents", "Info.plist");
    public string DesktopResourcesDirectory => System.IO.Path.Join(AppBundleDirectory, "Contents", "Resources");
    public string DesktopIconPath => System.IO.Path.Join(DesktopResourcesDirectory, BundleIconFile);
}
