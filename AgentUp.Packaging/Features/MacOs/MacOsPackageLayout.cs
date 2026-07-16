using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Features.MacOs;

public sealed record MacOsPackageLayout(
    string StageDirectory,
    string InstallerPublishDirectory,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory,
    string DesktopAppBundleDirectory,
    string DesktopAppContentsDirectory,
    string DesktopAppMacOsDirectory,
    string InstallerAppBundleDirectory,
    string InstallerAppContentsDirectory,
    string InstallerAppMacOsDirectory,
    string InstallerPayloadDirectory,
    string PackageRootDirectory,
    string ComponentPackageDirectory,
    string InstallerComponentRoot,
    string DesktopComponentRoot,
    string ServerComponentRoot,
    string CliComponentRoot,
    string ScriptsDirectory,
    string InstallerPackagePath,
    string DesktopPackagePath,
    string ServerPackagePath,
    string CliPackagePath,
    string ProductPackagePath,
    string DistributionXmlPath,
    string InstallerInfoPlistPath,
    string DesktopInfoPlistPath,
    string LaunchDaemonPlistPath,
    string PostInstallScriptPath,
    string PreInstallScriptPath)
{
    public static MacOsPackageLayout From(PackageRequest request)
    {
        var stage = request.StageDirectory;
        var desktopApp = Path.Combine(stage, "pkg-root", "desktop", "Applications", "Agent-Up.app");
        var installerApp = Path.Combine(stage, "pkg-root", "installer", "Applications", "Agent-Up Installer.app");
        return new MacOsPackageLayout(
            StageDirectory: stage,
            InstallerPublishDirectory: Path.Combine(stage, "installer"),
            DesktopPublishDirectory: Path.Combine(stage, "desktop"),
            ServerPublishDirectory: Path.Combine(stage, "server"),
            CliPublishDirectory: Path.Combine(stage, "cli"),
            DesktopAppBundleDirectory: desktopApp,
            DesktopAppContentsDirectory: Path.Combine(desktopApp, "Contents"),
            DesktopAppMacOsDirectory: Path.Combine(desktopApp, "Contents", "MacOS"),
            InstallerAppBundleDirectory: installerApp,
            InstallerAppContentsDirectory: Path.Combine(installerApp, "Contents"),
            InstallerAppMacOsDirectory: Path.Combine(installerApp, "Contents", "MacOS"),
            InstallerPayloadDirectory: Path.Combine(installerApp, "Contents", "MacOS", "payload"),
            PackageRootDirectory: Path.Combine(stage, "pkg-root"),
            ComponentPackageDirectory: Path.Combine(stage, "component-packages"),
            InstallerComponentRoot: Path.Combine(stage, "pkg-root", "installer"),
            DesktopComponentRoot: Path.Combine(stage, "pkg-root", "desktop"),
            ServerComponentRoot: Path.Combine(stage, "pkg-root", "server"),
            CliComponentRoot: Path.Combine(stage, "pkg-root", "cli"),
            ScriptsDirectory: Path.Combine(stage, "pkg-scripts"),
            InstallerPackagePath: Path.Combine(stage, "component-packages", "InstallerApp.pkg"),
            DesktopPackagePath: Path.Combine(stage, "component-packages", "DesktopApp.pkg"),
            ServerPackagePath: Path.Combine(stage, "component-packages", "Server.pkg"),
            CliPackagePath: Path.Combine(stage, "component-packages", "CLI.pkg"),
            ProductPackagePath: Path.Combine(request.OutputRoot, $"agent-up-macos-{request.RuntimeId}.pkg"),
            DistributionXmlPath: Path.Combine(stage, "Distribution.xml"),
            InstallerInfoPlistPath: Path.Combine(installerApp, "Contents", "Info.plist"),
            DesktopInfoPlistPath: Path.Combine(desktopApp, "Contents", "Info.plist"),
            LaunchDaemonPlistPath: Path.Combine(stage, "pkg-root", "server", "Library", "LaunchDaemons", "dev.agent-up.server.plist"),
            PostInstallScriptPath: Path.Combine(stage, "pkg-scripts", "postinstall"),
            PreInstallScriptPath: Path.Combine(stage, "pkg-scripts", "preinstall"));
    }
}
