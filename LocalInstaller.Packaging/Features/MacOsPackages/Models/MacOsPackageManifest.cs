using LocalInstaller.Core.Features.MacOsInstallation.Models;
using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.MacOsPackages.Models;

public sealed record MacOsPackageManifest(MacOsInstallerManifest InstallerManifest)
{
    public static MacOsPackageManifest From(PackageRequest request)
        => From(request, request.ProductManifest);

    public static MacOsPackageManifest From(PackageRequest request, PackageProductManifest product)
    {
        PackageProductManifest.Validate(product);
        return new(MacOsInstallerManifest.From(product.ProductName, product.Slug, request.NormalizedVersion) with
        {
            InstallerExecutableName = product.InstallerApplication?.ExecutableName ?? "installer",
            DesktopExecutableName = ArtifactExecutableName(product, LocalInstaller.Core.Shared.Models.LocalInstallerArtifactTarget.Desktop, "desktop"),
            ServerExecutableName = ArtifactExecutableName(product, LocalInstaller.Core.Shared.Models.LocalInstallerArtifactTarget.Server, "server")
        });
    }

    public MacOsInstallerManifest ToInstallerManifest()
        => InstallerManifest;

    private static string ArtifactExecutableName(
        PackageProductManifest product,
        LocalInstaller.Core.Shared.Models.LocalInstallerArtifactTarget target,
        string fallback)
        => product.InstallerOptions.FirstOrDefault(option => option.Target == target)?.ExecutableName ?? fallback;
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
