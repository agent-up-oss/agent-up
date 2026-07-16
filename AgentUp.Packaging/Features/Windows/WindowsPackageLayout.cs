using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Installers.Features.Windows;

namespace AgentUp.Packaging.Features.Windows;

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
        var installerSource = Path.Combine(stage, "wix");
        return new WindowsPackageLayout(
            StageDirectory: stage,
            InstallerSourceDirectory: installerSource,
            DesktopPublishDirectory: Path.Combine(stage, "desktop"),
            ServerPublishDirectory: Path.Combine(stage, "server"),
            CliPublishDirectory: Path.Combine(stage, "cli"),
            ProductWxsPath: Path.Combine(installerSource, "Product.wxs"),
            BundleWxsPath: Path.Combine(installerSource, "Bundle.wxs"),
            LicenseRtfPath: Path.Combine(installerSource, "License.rtf"),
            ProductMsiPath: Path.Combine(stage, "Product.msi"),
            ProductMsiOutputPath: Path.Combine(request.OutputRoot, $"agent-up-windows-{request.RuntimeId}.msi"),
            SetupExePath: Path.Combine(request.OutputRoot, $"agent-up-windows-{request.RuntimeId}.exe"));
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
