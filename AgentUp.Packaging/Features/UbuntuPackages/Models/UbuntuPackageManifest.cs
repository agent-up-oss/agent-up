using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Models;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.UbuntuPackages.Models;

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
        var installerManifest = UbuntuInstallerManifest.AgentUp();
        var paths = UbuntuInstallerPaths.ForProduct(installerManifest);
        return new UbuntuPackageManifest(
            PackageName: installerManifest.PackageName,
            Version: request.NormalizedVersion,
            Architecture: "amd64",
            Maintainer: "Agent-Up <ci@agent-up.local>",
            Description: "Local Agent-Up desktop, CLI, and server service.",
            ServiceName: installerManifest.ServiceUnitName,
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
        => UbuntuInstallerManifest.AgentUp().DesktopEntryText(UbuntuInstallerPaths.ForProduct(UbuntuInstallerManifest.AgentUp()).DesktopExecutable, Version);

    public static string PostInstallScript()
        => UbuntuInstallerManifest.PostInstallScript();

    public static string PreRemoveScript()
        => UbuntuInstallerManifest.PreRemoveScript();

    public static string PostRemoveScript()
        => UbuntuInstallerManifest.PostRemoveScript();
}
