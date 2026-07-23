using System.Security;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Shared.Providers;
using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.UbuntuPackages.Models;

public sealed record UbuntuPackageManifest(
    string PackageName,
    string ApplicationName,
    string Version,
    string Architecture,
    string Maintainer,
    string Description,
    string ServiceName)
{
    public static UbuntuPackageManifest From(PackageRequest request)
        => From(request, request.ProductManifest);

    public static UbuntuPackageManifest From(PackageRequest request, PackageProductManifest product)
    {
        PackageProductManifest.Validate(product);
        var packageName = PackagePathValidator.RequireSafePathComponent(product.Slug, nameof(PackageName));
        var serviceName = $"{packageName}-server.service";
        return new UbuntuPackageManifest(
            PackageName: packageName,
            ApplicationName: product.ProductName,
            Version: request.NormalizedVersion,
            Architecture: "amd64",
            Maintainer: $"{product.ProductName} <ci@{product.Slug}.local>",
            Description: $"Local {product.ProductName} installer.",
            ServiceName: serviceName);
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

    public string PostInstallScript()
        => $"""
           #!/usr/bin/env bash
           set -e
           chmod +x /opt/{PackageName}/installer/AgentUp.InstallerApp
           if command -v update-desktop-database >/dev/null 2>&1; then
             update-desktop-database /usr/share/applications 2>/dev/null || true
           fi
           if [ -n "$SUDO_USER" ] && [ "$SUDO_USER" != "root" ]; then
             su "$SUDO_USER" -c "/opt/{PackageName}/installer/AgentUp.InstallerApp &" 2>/dev/null || true
           fi
           """ + Environment.NewLine;

    public string InstallerDesktopEntryText()
        => $"""
           [Desktop Entry]
           Name={ApplicationName} Installer
           Comment={Description}
           Exec=/opt/{PackageName}/installer/AgentUp.InstallerApp
           Icon={PackageName}
           Type=Application
           Categories=System;
           """ + Environment.NewLine;

    public string PreRemoveScript()
        => $"""
           #!/usr/bin/env bash
           set -e
           /opt/{PackageName}/installer/AgentUp.InstallerApp --uninstall-component server 2>/dev/null || true
           /opt/{PackageName}/installer/AgentUp.InstallerApp --uninstall-component cli 2>/dev/null || true
           /opt/{PackageName}/installer/AgentUp.InstallerApp --uninstall-component desktop 2>/dev/null || true
           """ + Environment.NewLine;

    public string MetainfoText()
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var pkg = SecurityElement.Escape(PackageName)!;
        var app = SecurityElement.Escape(ApplicationName)!;
        var ver = SecurityElement.Escape(Version)!;
        return $"""
               <?xml version="1.0" encoding="UTF-8"?>
               <component type="desktop-application">
                 <id>{pkg}-installer.desktop</id>
                 <metadata_license>MIT</metadata_license>
                 <project_license>MIT</project_license>
                 <name>{app} Installer</name>
                 <summary>Local {app} installer</summary>
                 <description>
                   <p>Installer for {app}.</p>
                 </description>
                 <launchable type="desktop-id">{pkg}-installer.desktop</launchable>
                 <provides>
                   <pkgname>{pkg}</pkgname>
                 </provides>
                 <releases>
                   <release version="{ver}" date="{date}"/>
                 </releases>
                 <content_rating type="oars-1.1"/>
               </component>
               """ + Environment.NewLine;
    }

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
