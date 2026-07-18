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
    string InstallerAppBundleDirectory,
    string InstallerAppContentsDirectory,
    string InstallerAppMacOsDirectory,
    string InstallerPayloadDirectory,
    string PackageRootDirectory,
    string ComponentPackageDirectory,
    string InstallerComponentRoot,
    string InstallerScriptsDirectory,
    string InstallerPackagePath,
    string ProductPackagePath,
    string DistributionXmlPath,
    string InstallerInfoPlistPath)
{
    public static MacOsPackageLayout From(PackageRequest request)
    {
        var stage = request.StageDirectory;
        var installerApp = Path.Join(stage, "pkg-root", "installer", "Applications", "Agent-Up Installer.app");
        return new MacOsPackageLayout(
            StageDirectory: stage,
            InstallerPublishDirectory: Path.Join(stage, "installer"),
            DesktopPublishDirectory: Path.Join(stage, "desktop"),
            ServerPublishDirectory: Path.Join(stage, "server"),
            CliPublishDirectory: Path.Join(stage, "cli"),
            InstallerAppBundleDirectory: installerApp,
            InstallerAppContentsDirectory: Path.Join(installerApp, "Contents"),
            InstallerAppMacOsDirectory: Path.Join(installerApp, "Contents", "MacOS"),
            InstallerPayloadDirectory: Path.Join(installerApp, "Contents", "MacOS", "payload"),
            PackageRootDirectory: Path.Join(stage, "pkg-root"),
            ComponentPackageDirectory: Path.Join(stage, "component-packages"),
            InstallerComponentRoot: Path.Join(stage, "pkg-root", "installer"),
            InstallerScriptsDirectory: Path.Join(stage, "installer-pkg-scripts"),
            InstallerPackagePath: Path.Join(stage, "component-packages", "InstallerApp.pkg"),
            ProductPackagePath: Path.Join(request.OutputRoot, $"agent-up-macos-{request.RuntimeId}.pkg"),
            DistributionXmlPath: Path.Join(stage, "Distribution.xml"),
            InstallerInfoPlistPath: Path.Join(installerApp, "Contents", "Info.plist"));
    }
}
