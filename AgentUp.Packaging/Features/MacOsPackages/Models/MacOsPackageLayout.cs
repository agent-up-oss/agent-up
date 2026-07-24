using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.MacOsPackages.Models;

public sealed record MacOsPackageLayout(
    string StageDirectory,
    string InstallerPublishDirectory,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory,
    string TrayPublishDirectory,
    string InstallerAppBundleDirectory,
    string InstallerAppContentsDirectory,
    string InstallerAppMacOsDirectory,
    string InstallerAppResourcesDirectory,
    string InstallerPayloadDirectory,
    string InstallerPayloadIconDirectory,
    string PackageRootDirectory,
    string ComponentPackageDirectory,
    string InstallerComponentRoot,
    string InstallerScriptsDirectory,
    string InstallerPackagePath,
    string ProductPackagePath,
    string DistributionXmlPath,
    string InstallerInfoPlistPath,
    string InstallerIconSourcePath,
    string InstallerIconPath,
    string InstallerPayloadIconPath)
{
    public static MacOsPackageLayout From(PackageRequest request)
    {
        var stage = request.StageDirectory;
        var installerApp = Path.Join(stage, "pkg-root", "installer", "Applications", "Agent-Up Installer.app");
        var installerResources = Path.Join(installerApp, "Contents", "Resources");
        var payloadIcon = Path.Join(installerApp, "Contents", "MacOS", "payload", "icon");
        return new MacOsPackageLayout(
            StageDirectory: stage,
            InstallerPublishDirectory: Path.Join(stage, "installer"),
            DesktopPublishDirectory: Path.Join(stage, "desktop"),
            ServerPublishDirectory: Path.Join(stage, "server"),
            CliPublishDirectory: Path.Join(stage, "cli"),
            TrayPublishDirectory: Path.Join(stage, "tray"),
            InstallerAppBundleDirectory: installerApp,
            InstallerAppContentsDirectory: Path.Join(installerApp, "Contents"),
            InstallerAppMacOsDirectory: Path.Join(installerApp, "Contents", "MacOS"),
            InstallerAppResourcesDirectory: installerResources,
            InstallerPayloadDirectory: Path.Join(installerApp, "Contents", "MacOS", "payload"),
            InstallerPayloadIconDirectory: payloadIcon,
            PackageRootDirectory: Path.Join(stage, "pkg-root"),
            ComponentPackageDirectory: Path.Join(stage, "component-packages"),
            InstallerComponentRoot: Path.Join(stage, "pkg-root", "installer"),
            InstallerScriptsDirectory: Path.Join(stage, "installer-pkg-scripts"),
            InstallerPackagePath: Path.Join(stage, "component-packages", "InstallerApp.pkg"),
            ProductPackagePath: Path.Join(request.OutputRoot, $"agent-up-macos-{request.RuntimeId}.pkg"),
            DistributionXmlPath: Path.Join(stage, "Distribution.xml"),
            InstallerInfoPlistPath: Path.Join(installerApp, "Contents", "Info.plist"),
            InstallerIconSourcePath: Path.Join(request.RepositoryRoot, "media", "logo.png"),
            InstallerIconPath: Path.Join(installerResources, "Agent-Up.png"),
            InstallerPayloadIconPath: Path.Join(payloadIcon, "Agent-Up.png"));
    }
}
