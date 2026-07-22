using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.MacOsInstallation.Models;

public sealed record MacOsInstallerPaths(
    string AppBundleDirectory,
    string ApplicationSupportDirectory,
    string ServerDirectory,
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
            CliDirectory: "/usr/local/agent-up/cli",
            LaunchDaemonPath: "/Library/LaunchDaemons/dev.agent-up.server.plist",
            LogsDirectory: "/Library/Logs/Agent-Up",
            CliSymlinkPath: "/usr/local/bin/agent-up",
            ServerSymlinkPath: "/usr/local/bin/agent-up-server",
            DesktopSymlinkPath: "/usr/local/bin/agent-up-desktop",
            BundleIconFile: "Agent-Up.png");

    public static MacOsInstallerPaths From(ProductManifest product)
        => new(
            AppBundleDirectory: $"/Applications/{product.ProductName}.app",
            ApplicationSupportDirectory: $"/Library/Application Support/{product.ProductName}",
            ServerDirectory: $"/Library/Application Support/{product.ProductName}/server",
            CliDirectory: $"/usr/local/{product.Slug}/cli",
            LaunchDaemonPath: $"/Library/LaunchDaemons/dev.{product.Slug}.server.plist",
            LogsDirectory: $"/Library/Logs/{product.ProductName}",
            CliSymlinkPath: $"/usr/local/bin/{product.Slug}",
            ServerSymlinkPath: $"/usr/local/bin/{product.Slug}-server",
            DesktopSymlinkPath: $"/usr/local/bin/{product.Slug}-desktop",
            BundleIconFile: $"{product.ProductName.Replace(" ", "-")}.png");

    public string DesktopExecutable => System.IO.Path.Join(AppBundleDirectory, "Contents", "MacOS", "AgentUp.Desktop");
    public string ServerExecutable => System.IO.Path.Join(ServerDirectory, "AgentUp.Server");
    public string CliExecutable => System.IO.Path.Join(CliDirectory, "AgentUp.CLI");
    public string DesktopInfoPlistPath => System.IO.Path.Join(AppBundleDirectory, "Contents", "Info.plist");
    public string DesktopResourcesDirectory => System.IO.Path.Join(AppBundleDirectory, "Contents", "Resources");
    public string DesktopIconPath => System.IO.Path.Join(DesktopResourcesDirectory, BundleIconFile);
}
