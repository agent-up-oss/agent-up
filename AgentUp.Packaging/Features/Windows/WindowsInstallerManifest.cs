using AgentUp.Installers.Features.Windows;
using AgentUp.Packaging.Features.ReleaseArtifacts;

namespace AgentUp.Packaging.Features.Windows;

public sealed record WindowsPackageManifest(WindowsInstallerManifest InstallerManifest)
{
    public static WindowsPackageManifest From(PackageRequest request)
        => new(WindowsInstallerManifest.Create(request.WindowsInstallerVersion));
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
        => AgentUp.Installers.Features.Windows.WindowsWixSourceGenerator.LicenseRtf();

    public static string CliShimText()
        => AgentUp.Installers.Features.Windows.WindowsWixSourceGenerator.CliShimText();

    private AgentUp.Installers.Features.Windows.WindowsWixSourceGenerator Generator()
        => new(_manifest.InstallerManifest);
}
