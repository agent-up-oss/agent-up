using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Models;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.MacOsPackages.Models;

public sealed record MacOsPackageManifest(MacOsInstallerManifest InstallerManifest)
{
    public static MacOsPackageManifest From(PackageRequest request)
        => From(request, request.ProductManifest);

    public static MacOsPackageManifest From(PackageRequest request, PackageProductManifest product)
    {
        PackageProductManifest.Validate(product);
        return new(MacOsInstallerManifest.From(
            new ProductManifest(product.ProductName, product.Slug, product.EnvironmentPrefix),
            request.NormalizedVersion));
    }

    public MacOsInstallerManifest ToInstallerManifest()
        => InstallerManifest;
}

public sealed class MacOsPlistGenerator
{
    private readonly MacOsPackageManifest _manifest;

    public MacOsPlistGenerator(MacOsPackageManifest manifest)
    {
        _manifest = manifest;
    }

    public string DesktopInfoPlist()
        => Generator().DesktopInfoPlist();

    public string InstallerInfoPlist()
        => Generator().InstallerInfoPlist();

    public string LaunchDaemonPlist()
        => Generator().LaunchDaemonPlist();

    private MacOsInstallerPlistGenerator Generator()
        => new(_manifest.InstallerManifest);
}
