using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.MacOs.Models;

public sealed record MacOsPackageLayout(
    string StageDirectory,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory,
    string AppBundleDirectory,
    string AppContentsDirectory,
    string AppMacOsDirectory,
    string PackageRootDirectory,
    string ComponentPackageDirectory,
    string DesktopComponentRoot,
    string ServerComponentRoot,
    string CliComponentRoot,
    string ScriptsDirectory,
    string DesktopPackagePath,
    string ServerPackagePath,
    string CliPackagePath,
    string ProductPackagePath,
    string DistributionXmlPath,
    string LaunchDaemonPlistPath,
    string PostInstallScriptPath,
    string PreInstallScriptPath)
{
    public static MacOsPackageLayout From(PackageRequest request)
    {
        var stage = request.StageDirectory;
        var app = Path.Join(stage, "Agent-Up.app");
        return new MacOsPackageLayout(
            StageDirectory: stage,
            DesktopPublishDirectory: Path.Join(stage, "desktop"),
            ServerPublishDirectory: Path.Join(stage, "server"),
            CliPublishDirectory: Path.Join(stage, "cli"),
            AppBundleDirectory: app,
            AppContentsDirectory: Path.Join(app, "Contents"),
            AppMacOsDirectory: Path.Join(app, "Contents", "MacOS"),
            PackageRootDirectory: Path.Join(stage, "pkg-root"),
            ComponentPackageDirectory: Path.Join(stage, "component-packages"),
            DesktopComponentRoot: Path.Join(stage, "pkg-root", "desktop"),
            ServerComponentRoot: Path.Join(stage, "pkg-root", "server"),
            CliComponentRoot: Path.Join(stage, "pkg-root", "cli"),
            ScriptsDirectory: Path.Join(stage, "pkg-scripts"),
            DesktopPackagePath: Path.Join(stage, "component-packages", "DesktopApp.pkg"),
            ServerPackagePath: Path.Join(stage, "component-packages", "Server.pkg"),
            CliPackagePath: Path.Join(stage, "component-packages", "CLI.pkg"),
            ProductPackagePath: Path.Join(request.OutputRoot, $"agent-up-macos-{request.RuntimeId}.pkg"),
            DistributionXmlPath: Path.Join(stage, "Distribution.xml"),
            LaunchDaemonPlistPath: Path.Join(stage, "pkg-root", "server", "Library", "LaunchDaemons", "dev.agent-up.server.plist"),
            PostInstallScriptPath: Path.Join(stage, "pkg-scripts", "postinstall"),
            PreInstallScriptPath: Path.Join(stage, "pkg-scripts", "preinstall"));
    }
}
