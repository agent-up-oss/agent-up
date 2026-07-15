using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Installers.Features.Ubuntu;

namespace AgentUp.Packaging.Features.Ubuntu;

public sealed record UbuntuPackageManifest(
    string PackageName,
    string Version,
    string Architecture,
    string Maintainer,
    string Description,
    string ServiceName,
    string CliSymlinkTarget,
    string DesktopEntryPath,
    string IconPath)
{
    public static UbuntuPackageManifest From(PackageRequest request)
    {
        var paths = UbuntuInstallerPaths.SystemDefault();
        return new UbuntuPackageManifest(
            PackageName: UbuntuInstallerManifest.PackageName,
            Version: request.NormalizedVersion,
            Architecture: "amd64",
            Maintainer: "Agent-Up <ci@agent-up.local>",
            Description: "Local Agent-Up desktop, CLI, and server service.",
            ServiceName: UbuntuInstallerManifest.ServiceName,
            CliSymlinkTarget: paths.CliExecutable,
            DesktopEntryPath: paths.DesktopEntryPath,
            IconPath: paths.IconPath);
    }

    public string ControlFileText()
        => $"""
           Package: {PackageName}
           Version: {Version}
           Section: devel
           Priority: optional
           Architecture: {Architecture}
           Maintainer: {Maintainer}
           Description: {Description}
           """ + Environment.NewLine;

    public string DesktopEntryText()
        => UbuntuInstallerManifest.DesktopEntryText(Version);

    public static string PostInstallScript()
        => UbuntuInstallerManifest.PostInstallScript();

    public static string PreRemoveScript()
        => UbuntuInstallerManifest.PreRemoveScript();

    public static string PostRemoveScript()
        => UbuntuInstallerManifest.PostRemoveScript();
}
