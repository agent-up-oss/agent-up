using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.UbuntuInstallation.Models;

public sealed record UbuntuInstallerManifest(
    string PackageName,
    string ServiceUnitName,
    string CliCommandName,
    string DesktopApplicationName)
{
    public static UbuntuInstallerManifest AgentUp()
        => new("agent-up", "agent-up-server.service", "agent-up", "Agent-Up");

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
               X-{versionKey}-Version={version}
               """ + Environment.NewLine;
    }

    public static string PostInstallScript()
        => """
           #!/usr/bin/env bash
           set -e
           mkdir -p /var/lib/agent-up
           touch /var/log/agent-up-server.log /var/log/agent-up-server.err.log
           chmod +x /opt/agent-up/desktop/AgentUp.Desktop /opt/agent-up/server/AgentUp.Server /opt/agent-up/cli/AgentUp.CLI
           systemctl daemon-reload
           systemctl enable --now agent-up-server.service
           if command -v update-desktop-database >/dev/null 2>&1; then
             update-desktop-database /usr/share/applications || true
           fi
           """ + Environment.NewLine;

    public static string PreRemoveScript()
        => """
           #!/usr/bin/env bash
           set -e
           systemctl disable --now agent-up-server.service 2>/dev/null || true
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
