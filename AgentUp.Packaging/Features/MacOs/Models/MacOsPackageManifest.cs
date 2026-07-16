using AgentUp.Installers.Features.MacOs.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.MacOs.Models;

public sealed record MacOsPackageManifest(MacOsInstallerManifest InstallerManifest)
{
    public static MacOsPackageManifest From(PackageRequest request)
        => new(MacOsInstallerManifest.Create(request.NormalizedVersion));

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
