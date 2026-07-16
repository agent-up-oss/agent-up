using AgentUp.Installers.Features.Windows.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.Windows.Models;

public sealed record WindowsPackageManifest(WindowsInstallerManifest InstallerManifest)
{
    public static WindowsPackageManifest From(PackageRequest request)
        => new(WindowsInstallerManifest.Create(request.NormalizedVersion));
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
        => Installers.Features.Windows.Models.WindowsWixSourceGenerator.LicenseRtf();

    public static string CliShimText()
        => Installers.Features.Windows.Models.WindowsWixSourceGenerator.CliShimText();

    private Installers.Features.Windows.Models.WindowsWixSourceGenerator Generator()
        => new(_manifest.InstallerManifest);
}
