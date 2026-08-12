namespace LocalInstaller.Packaging.Features.MacOsPackages.Models;

public static class MacOsScriptGenerator
{
    public static string InstallerPreInstallScript(string installerAppName)
        => $"""
            #!/usr/bin/env bash
            set -euo pipefail
            rm -rf "/Applications/{installerAppName}.app"
            CONSOLE_USER=$(stat -f %Su /dev/console 2>/dev/null || true)
            if [ -n "$CONSOLE_USER" ] && [ "$CONSOLE_USER" != "root" ] && \
               [[ "$CONSOLE_USER" =~ ^[a-zA-Z0-9._-]+$ ]]; then
                rm -rf "/Users/$CONSOLE_USER/.net/LocalInstaller.App" 2>/dev/null || true
            fi
            """ + Environment.NewLine;

    public static string InstallerPostInstallScript(string installerAppName, string logDirectory)
        => $"""
            #!/usr/bin/env bash
            set -euo pipefail
            open -a "/Applications/{installerAppName}.app" 2>>"{logDirectory}/installer-startup.err" || true
            """ + Environment.NewLine;
}
