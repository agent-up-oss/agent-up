namespace AgentUp.Installers.Features.MacOsInstallation.Services;

public static class MacOsInstallerScripts
{
    public static string InstallerPreInstallScript()
        => """
           #!/usr/bin/env bash
           set -euo pipefail
           rm -rf "/Applications/Agent-Up Installer.app"
           CONSOLE_USER=$(stat -f %Su /dev/console 2>/dev/null || true)
           if [ -n "$CONSOLE_USER" ] && [ "$CONSOLE_USER" != "root" ]; then
               rm -rf "/Users/$CONSOLE_USER/.net/AgentUp.InstallerApp" 2>/dev/null || true
           fi
           """ + Environment.NewLine;

    public static string InstallerPostInstallScript()
        => """
           #!/usr/bin/env bash
           set -euo pipefail
           open -a "/Applications/Agent-Up Installer.app" 2>/dev/null || true
           """ + Environment.NewLine;
}
