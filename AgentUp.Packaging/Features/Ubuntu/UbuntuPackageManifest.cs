using AgentUp.Packaging.Features.ReleaseArtifacts;

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
        => new(
            PackageName: "agent-up",
            Version: request.NormalizedVersion,
            Architecture: "amd64",
            Maintainer: "Agent-Up <ci@agent-up.local>",
            Description: "Local Agent-Up desktop, CLI, and server service.",
            ServiceName: "agent-up-server.service",
            CliSymlinkTarget: "/opt/agent-up/cli/AgentUp.CLI",
            DesktopEntryPath: "/usr/share/applications/agent-up.desktop",
            IconPath: "/usr/share/pixmaps/agent-up.png");

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
        => $"""
           [Desktop Entry]
           Type=Application
           Name=Agent-Up
           Comment=Agent-Up desktop workspace client
           Exec=/opt/agent-up/desktop/AgentUp.Desktop
           Icon=agent-up
           Terminal=false
           Categories=Development;
           StartupNotify=true
           X-AgentUp-Version={Version}
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
