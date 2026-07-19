namespace AgentUp.Installers.Features.MacOsInstallation.Services;

public static class MacOsInstallerScripts
{
    public static string InstallerPreInstallScript()
        => """
           #!/usr/bin/env bash
           set -euo pipefail
           rm -rf "/Applications/Agent-Up Installer.app"
           """ + Environment.NewLine;

    public static string InstallerPostInstallScript()
        => """
           #!/usr/bin/env bash
           set -euo pipefail
           INSTALLER="/Applications/Agent-Up Installer.app/Contents/MacOS/AgentUp.InstallerApp"
           PAYLOAD_ROOT="/Applications/Agent-Up Installer.app/Contents/MacOS/payload"
           if [ -x "$INSTALLER" ]; then
               "$INSTALLER" --install-core --payload-root "$PAYLOAD_ROOT" || true
           fi
           open -a "/Applications/Agent-Up Installer.app" 2>/dev/null || true
           """ + Environment.NewLine;
}
