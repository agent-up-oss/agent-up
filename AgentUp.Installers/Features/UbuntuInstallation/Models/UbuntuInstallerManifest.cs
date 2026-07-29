using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.UbuntuInstallation.Models;

public sealed partial record UbuntuInstallerManifest(
    string PackageName,
    string ServiceUnitName,
    string CliCommandName,
    string DesktopApplicationName)
{
    public static UbuntuInstallerManifest ForProduct(ProductManifest manifest)
        => new(
            PackageName: manifest.Slug,
            ServiceUnitName: $"{manifest.ServiceName}.service",
            CliCommandName: manifest.CliCommandName,
            DesktopApplicationName: manifest.ProductName);

    public string DesktopEntryText(string executablePath, string version)
    {
        var versionKey = DesktopApplicationName.Replace("-", "").Replace(" ", "");
        return $"""
               [Desktop Entry]
               Type=Application
               Name={DesktopApplicationName}
               Comment={DesktopApplicationName} desktop workspace client
               Exec={executablePath}
               Icon={PackageName}
               Terminal=false
               Categories=Development;
               StartupNotify=true
               StartupWMClass=AgentUp.Desktop
               X-{versionKey}-Version={version}
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
           systemctl enable --now {ServiceUnitName}
           if command -v update-desktop-database >/dev/null 2>&1; then
             update-desktop-database /usr/share/applications || true
           fi
           """ + Environment.NewLine;

    public string PreRemoveScript()
        => $"""
           #!/usr/bin/env bash
           set -e
           systemctl disable --now {ServiceUnitName} 2>/dev/null || true
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
