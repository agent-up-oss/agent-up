namespace AgentUp.Installers.Features.Ubuntu;

public static class UbuntuInstallerManifest
{
    public static string DesktopEntryText(Version version)
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
           X-AgentUp-Version={version}
           """ + Environment.NewLine;
}
