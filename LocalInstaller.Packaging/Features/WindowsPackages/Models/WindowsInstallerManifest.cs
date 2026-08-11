using LocalInstaller.Core.Features.WindowsInstallation.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.WindowsPackages.Models;

public sealed record WindowsPackageManifest(WindowsInstallerManifest InstallerManifest)
{
    public static WindowsPackageManifest From(PackageRequest request)
        => new(new WindowsInstallerManifest(
            ProductName: request.ProductManifest.ProductName,
            Manufacturer: request.ProductManifest.Manufacturer ?? request.ProductManifest.ProductName,
            Version: request.WindowsInstallerVersion,
            UpgradeCode: request.ProductManifest.WindowsUpgradeCode ?? StableUpgradeCode(request.ProductManifest.Slug),
            ServiceName: request.ProductManifest.WindowsServiceName ?? $"{request.ProductManifest.Slug}-server",
            CliShimName: request.ProductManifest.WindowsCliShimName ?? $"{request.ProductManifest.Slug}.cmd",
            BundleName: request.ProductManifest.ProductName,
            ServerUrl: request.ProductManifest.WindowsServerUrl ?? "http://127.0.0.1:5000"));

    private static string StableUpgradeCode(string slug)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes("Windows Installer Upgrade Code:" + slug));
        return new Guid(bytes).ToString("D").ToUpperInvariant();
    }
}

public sealed class WindowsWixSourceGenerator
{
    private readonly WindowsPackageManifest _manifest;

    public WindowsWixSourceGenerator(WindowsPackageManifest manifest)
    {
        _manifest = manifest;
    }

    public string ProductWxs(WindowsPackageLayout layout)
        => Generator().ProductWxs(layout.ToInstallerLayout());

    public string BundleWxs(WindowsPackageLayout layout)
        => Generator().BundleWxs(layout.ToInstallerLayout());

    public static string LicenseRtf(string productName = "Agent-Up")
        => Core.Features.WindowsInstallation.Models.WindowsWixSourceGenerator.LicenseRtf(productName);

    public static string CliShimText()
        => Core.Features.WindowsInstallation.Models.WindowsWixSourceGenerator.CliShimText();

    private Core.Features.WindowsInstallation.Models.WindowsWixSourceGenerator Generator()
        => new(_manifest.InstallerManifest);
}
