using AgentUp.Installers.Features.Windows.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.Windows.Models;

public sealed record WindowsPackageLayout(
    string StageDirectory,
    string InstallerSourceDirectory,
    string DesktopPublishDirectory,
    string ServerPublishDirectory,
    string CliPublishDirectory,
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
            DesktopPublishDirectory: Path.Join(stage, "desktop"),
            ServerPublishDirectory: Path.Join(stage, "server"),
            CliPublishDirectory: Path.Join(stage, "cli"),
            ProductWxsPath: Path.Join(installerSource, "Product.wxs"),
            BundleWxsPath: Path.Join(installerSource, "Bundle.wxs"),
            LicenseRtfPath: Path.Join(installerSource, "License.rtf"),
            ProductMsiPath: Path.Join(stage, "Product.msi"),
            ProductMsiOutputPath: Path.Join(request.OutputRoot, $"agent-up-windows-{request.RuntimeId}.msi"),
            SetupExePath: Path.Join(request.OutputRoot, $"agent-up-windows-{request.RuntimeId}.exe"));
    }

    public WindowsInstallerLayout ToInstallerLayout()
        => new(
            InstallerSourceDirectory,
            DesktopPublishDirectory,
            ServerPublishDirectory,
            CliPublishDirectory,
            LicenseRtfPath,
            ProductMsiPath);
}
