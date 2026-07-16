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
        var desktopApp = Path.Join(stage, "pkg-root", "desktop", "Applications", "Agent-Up.app");
        var installerApp = Path.Join(stage, "pkg-root", "installer", "Applications", "Agent-Up Installer.app");
        return new MacOsPackageLayout(
            StageDirectory: stage,
            InstallerPublishDirectory: Path.Join(stage, "installer"),
            DesktopPublishDirectory: Path.Join(stage, "desktop"),
            ServerPublishDirectory: Path.Join(stage, "server"),
            CliPublishDirectory: Path.Join(stage, "cli"),
            DesktopAppBundleDirectory: desktopApp,
            DesktopAppContentsDirectory: Path.Join(desktopApp, "Contents"),
            DesktopAppMacOsDirectory: Path.Join(desktopApp, "Contents", "MacOS"),
            InstallerAppBundleDirectory: installerApp,
            InstallerAppContentsDirectory: Path.Join(installerApp, "Contents"),
            InstallerAppMacOsDirectory: Path.Join(installerApp, "Contents", "MacOS"),
            InstallerPayloadDirectory: Path.Join(installerApp, "Contents", "MacOS", "payload"),
            PackageRootDirectory: Path.Join(stage, "pkg-root"),
            ComponentPackageDirectory: Path.Join(stage, "component-packages"),
            InstallerComponentRoot: Path.Join(stage, "pkg-root", "installer"),
            DesktopComponentRoot: Path.Join(stage, "pkg-root", "desktop"),
            ServerComponentRoot: Path.Join(stage, "pkg-root", "server"),
            CliComponentRoot: Path.Join(stage, "pkg-root", "cli"),
            ScriptsDirectory: Path.Join(stage, "pkg-scripts"),
            InstallerPackagePath: Path.Join(stage, "component-packages", "InstallerApp.pkg"),
            DesktopPackagePath: Path.Join(stage, "component-packages", "DesktopApp.pkg"),
            ServerPackagePath: Path.Join(stage, "component-packages", "Server.pkg"),
            CliPackagePath: Path.Join(stage, "component-packages", "CLI.pkg"),
            ProductPackagePath: Path.Join(request.OutputRoot, $"agent-up-macos-{request.RuntimeId}.pkg"),
            DistributionXmlPath: Path.Join(stage, "Distribution.xml"),
            InstallerInfoPlistPath: Path.Join(installerApp, "Contents", "Info.plist"),
            DesktopInfoPlistPath: Path.Join(desktopApp, "Contents", "Info.plist"),
            LaunchDaemonPlistPath: Path.Join(stage, "pkg-root", "server", "Library", "LaunchDaemons", "dev.agent-up.server.plist"),
            PostInstallScriptPath: Path.Join(stage, "pkg-scripts", "postinstall"),
            PreInstallScriptPath: Path.Join(stage, "pkg-scripts", "preinstall"));
    }
}
