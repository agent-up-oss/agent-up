namespace AgentUp.Installers.Features.Ubuntu.Models;

public static class UbuntuInstallerManifest
{
    public const string PackageName = "agent-up";
    public const string ServiceName = "agent-up-server.service";
    public const string DesktopApplicationName = "Agent-Up";

    public static string DesktopEntryText(Version version)
        => DesktopEntryText(version.ToString());

    public static string DesktopEntryText(string version)
        => $"""
           [Desktop Entry]
           Type=Application
           Name={DesktopApplicationName}
           Comment=Agent-Up desktop workspace client
           Exec=/opt/agent-up/desktop/AgentUp.Desktop
           Icon=agent-up
           Terminal=false
           Categories=Development;
           StartupNotify=true
           X-AgentUp-Version={version}
           """ + Environment.NewLine;

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
