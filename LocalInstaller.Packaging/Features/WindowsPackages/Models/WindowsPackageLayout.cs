using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.WindowsPackages.Models;

public sealed record WindowsPackageLayout(
    string StageDirectory,
    string InstallerSourceDirectory,
    string InstallerPublishDirectory,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory,
    string TrayPublishDirectory,
    string ProductWxsPath,
    string BundleWxsPath,
    string LicenseRtfPath,
    string ProductMsiPath,
    string ProductMsiOutputPath,
    string SetupExePath)
{
    public static WindowsPackageLayout From(PackageRequest request)
    {
        var stage = request.StageDirectory;
        var installerSource = Path.Join(stage, "wix");
        return new WindowsPackageLayout(
            StageDirectory: stage,
            InstallerSourceDirectory: installerSource,
            InstallerPublishDirectory: Path.Join(stage, "installer"),
            DesktopPublishDirectory: Path.Join(stage, "desktop"),
            ServerPublishDirectory: Path.Join(stage, "server"),
            CliPublishDirectory: Path.Join(stage, "cli"),
            TrayPublishDirectory: Path.Join(stage, "tray"),
            ProductWxsPath: Path.Join(installerSource, "Product.wxs"),
            BundleWxsPath: Path.Join(installerSource, "Bundle.wxs"),
            LicenseRtfPath: Path.Join(installerSource, "License.rtf"),
            ProductMsiPath: Path.Join(stage, "Product.msi"),
            ProductMsiOutputPath: Path.Join(request.OutputRoot, $"{request.ProductManifest.Slug}-windows-{request.RuntimeId}.msi"),
            SetupExePath: Path.Join(request.OutputRoot, $"{request.ProductManifest.Slug}-windows-{request.RuntimeId}.exe"));
    }

    public WindowsInstallerLayout ToInstallerLayout()
        => new(
            InstallerSourceDirectory,
            InstallerPublishDirectory,
            DesktopPublishDirectory,
            ServerPublishDirectory,
            CliPublishDirectory,
            TrayPublishDirectory,
            LicenseRtfPath,
            ProductMsiPath);
}
