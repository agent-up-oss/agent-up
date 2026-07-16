namespace AgentUp.Installers.Features.MacOs;

public static class MacOsInstallerScripts
{
    public static string PreInstallScript()
        => """
           #!/usr/bin/env bash
           set -euo pipefail
           launchctl bootout system /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true
           """ + Environment.NewLine;

    public static string PostInstallScript()
        => """
           #!/usr/bin/env bash
           mkdir -p "/Library/Application Support/Agent-Up" || true
           mkdir -p "/Library/Logs/Agent-Up" || true
           chmod +x /usr/local/agent-up/desktop/AgentUp.Desktop 2>/dev/null || true
           chmod +x "/Library/Application Support/Agent-Up/server/AgentUp.Server" 2>/dev/null || true
           chmod +x /usr/local/agent-up/cli/AgentUp.CLI 2>/dev/null || true
           ln -sf /usr/local/agent-up/cli/AgentUp.CLI /usr/local/bin/agent-up
           ln -sf "/Library/Application Support/Agent-Up/server/AgentUp.Server" /usr/local/bin/agent-up-server
           ln -sf /usr/local/agent-up/desktop/AgentUp.Desktop /usr/local/bin/agent-up-desktop
           chown root:wheel /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true
           chmod 644 /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true
           launchctl bootstrap system /Library/LaunchDaemons/dev.agent-up.server.plist 2>/dev/null || true
           """ + Environment.NewLine;
}
