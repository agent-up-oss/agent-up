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
           open -a "/Applications/Agent-Up Installer.app" 2>/dev/null || true
           """ + Environment.NewLine;
}
