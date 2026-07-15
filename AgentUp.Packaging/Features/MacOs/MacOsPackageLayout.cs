using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Features.MacOs;

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
        var app = Path.Combine(stage, "Agent-Up.app");
        return new MacOsPackageLayout(
            StageDirectory: stage,
            DesktopPublishDirectory: Path.Combine(stage, "desktop"),
            ServerPublishDirectory: Path.Combine(stage, "server"),
            CliPublishDirectory: Path.Combine(stage, "cli"),
            AppBundleDirectory: app,
            AppContentsDirectory: Path.Combine(app, "Contents"),
            AppMacOsDirectory: Path.Combine(app, "Contents", "MacOS"),
            PackageRootDirectory: Path.Combine(stage, "pkg-root"),
            ComponentPackageDirectory: Path.Combine(stage, "component-packages"),
            DesktopComponentRoot: Path.Combine(stage, "pkg-root", "desktop"),
            ServerComponentRoot: Path.Combine(stage, "pkg-root", "server"),
            CliComponentRoot: Path.Combine(stage, "pkg-root", "cli"),
            ScriptsDirectory: Path.Combine(stage, "pkg-scripts"),
            DesktopPackagePath: Path.Combine(stage, "component-packages", "DesktopApp.pkg"),
            ServerPackagePath: Path.Combine(stage, "component-packages", "Server.pkg"),
            CliPackagePath: Path.Combine(stage, "component-packages", "CLI.pkg"),
            ProductPackagePath: Path.Combine(request.OutputRoot, $"agent-up-macos-{request.RuntimeId}.pkg"),
            DistributionXmlPath: Path.Combine(stage, "Distribution.xml"),
            LaunchDaemonPlistPath: Path.Combine(stage, "pkg-root", "server", "Library", "LaunchDaemons", "dev.agent-up.server.plist"),
            PostInstallScriptPath: Path.Combine(stage, "pkg-scripts", "postinstall"),
            PreInstallScriptPath: Path.Combine(stage, "pkg-scripts", "preinstall"));
    }
}
