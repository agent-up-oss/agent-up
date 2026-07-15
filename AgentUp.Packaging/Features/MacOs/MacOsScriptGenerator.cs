namespace AgentUp.Packaging.Features.MacOs;

public static class MacOsScriptGenerator
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
           set -euo pipefail

           mkdir -p "/Library/Application Support/Agent-Up"
           mkdir -p "/Library/Logs/Agent-Up"
           chmod +x /Applications/Agent-Up.app/Contents/MacOS/AgentUp.Desktop
           chmod +x "/Library/Application Support/Agent-Up/server/AgentUp.Server"
           chmod +x /usr/local/agent-up/cli/AgentUp.CLI
           ln -sf /usr/local/agent-up/cli/AgentUp.CLI /usr/local/bin/agent-up
           ln -sf "/Library/Application Support/Agent-Up/server/AgentUp.Server" /usr/local/bin/agent-up-server
           ln -sf /Applications/Agent-Up.app/Contents/MacOS/AgentUp.Desktop /usr/local/bin/agent-up-desktop
           chown root:wheel /Library/LaunchDaemons/dev.agent-up.server.plist
           chmod 644 /Library/LaunchDaemons/dev.agent-up.server.plist
           launchctl bootstrap system /Library/LaunchDaemons/dev.agent-up.server.plist
           launchctl kickstart -k system/dev.agent-up.server
           """ + Environment.NewLine;
}
