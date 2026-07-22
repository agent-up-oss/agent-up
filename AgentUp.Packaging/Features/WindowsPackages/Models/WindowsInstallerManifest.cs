using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Installers.Features.WindowsInstallation.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.WindowsPackages.Models;

public sealed record WindowsPackageManifest(WindowsInstallerManifest InstallerManifest)
{
    public static WindowsPackageManifest From(PackageRequest request)
        => new(request.ProductManifest.Slug.Equals("agent-up", StringComparison.Ordinal)
            ? WindowsInstallerManifest.Create(request.WindowsInstallerVersion)
            : WindowsInstallerManifest.From(request.ProductManifest, request.WindowsInstallerVersion, "http://127.0.0.1:5000"));
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

    public static string LicenseRtf()
        => Installers.Features.WindowsInstallation.Models.WindowsWixSourceGenerator.LicenseRtf();

    public static string CliShimText()
        => Installers.Features.WindowsInstallation.Models.WindowsWixSourceGenerator.CliShimText();

    private Installers.Features.WindowsInstallation.Models.WindowsWixSourceGenerator Generator()
        => new(_manifest.InstallerManifest);
}
