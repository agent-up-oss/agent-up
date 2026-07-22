using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Shared.Providers;
using AgentUp.Installers.Features.UbuntuInstallation.Models;
using AgentUp.Installers.Features.Installation.Models;
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
    string IconPath,
    string ApplicationName)
{
    public static UbuntuPackageManifest From(PackageRequest request)
        => From(request, ProductManifest.AgentUp());

    public static UbuntuPackageManifest From(PackageRequest request, ProductManifest product)
    {
        var installerManifest = UbuntuInstallerManifest.ForProduct(product);
        PackagePathValidator.RequireSafePathComponent(installerManifest.PackageName, nameof(PackageName));
        var paths = UbuntuInstallerPaths.ForProduct(installerManifest);
        return new UbuntuPackageManifest(
            PackageName: installerManifest.PackageName,
            Version: request.NormalizedVersion,
            Architecture: "amd64",
            Maintainer: $"{product.ProductName} <ci@{product.Slug}.local>",
            Description: $"Local {product.ProductName} desktop, CLI, and server service.",
            ServiceName: installerManifest.ServiceUnitName,
            CliSymlinkTarget: paths.CliExecutable,
            DesktopEntryPath: paths.DesktopEntryPath,
            IconPath: paths.IconPath,
            ApplicationName: installerManifest.DesktopApplicationName);
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
    {
        var versionKey = ApplicationName.Replace("-", "").Replace(" ", "");
        return $"""
               [Desktop Entry]
               Type=Application
               Name={ApplicationName}
               Comment={ApplicationName} desktop workspace client
               Exec=/opt/{PackageName}/desktop/AgentUp.Desktop
               Icon={PackageName}
               Terminal=false
               Categories=Development;
               StartupNotify=true
               X-{versionKey}-Version={Version}
               """ + Environment.NewLine;
    }

    public string PostInstallScript()
        => $"""
           #!/usr/bin/env bash
           set -e
           mkdir -p /var/lib/{PackageName}
           touch /var/log/{PackageName}-server.log /var/log/{PackageName}-server.err.log
           chmod +x /opt/{PackageName}/desktop/AgentUp.Desktop /opt/{PackageName}/server/AgentUp.Server /opt/{PackageName}/cli/AgentUp.CLI
           systemctl daemon-reload
           systemctl enable --now {ServiceName}
           if command -v update-desktop-database >/dev/null 2>&1; then
             update-desktop-database /usr/share/applications || true
           fi
           """ + Environment.NewLine;

    public string PreRemoveScript()
        => $"""
           #!/usr/bin/env bash
           set -e
           systemctl disable --now {ServiceName} 2>/dev/null || true
           """ + Environment.NewLine;

    public static string PostRemoveScript()
        => """
           #!/usr/bin/env bash
           set -e
           systemctl daemon-reload
           if command -v update-desktop-database >/dev/null 2>&1; then
             update-desktop-database /usr/share/applications || true
           fi
           """ + Environment.NewLine;
}
