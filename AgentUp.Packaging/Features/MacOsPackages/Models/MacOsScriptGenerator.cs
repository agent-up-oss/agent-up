using AgentUp.Installers.Features.MacOsInstallation.Services;

namespace AgentUp.Packaging.Features.MacOsPackages.Models;

public static class MacOsScriptGenerator
{
    public static string PreInstallScript()
        => MacOsInstallerScripts.PreInstallScript();

    public static string PostInstallScript()
        => MacOsInstallerScripts.PostInstallScript();
}
