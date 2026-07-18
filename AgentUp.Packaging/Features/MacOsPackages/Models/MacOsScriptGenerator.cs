using AgentUp.Installers.Features.MacOsInstallation.Services;

namespace AgentUp.Packaging.Features.MacOsPackages.Models;

public static class MacOsScriptGenerator
{
    public static string InstallerPreInstallScript()
        => MacOsInstallerScripts.InstallerPreInstallScript();

    public static string InstallerPostInstallScript()
        => MacOsInstallerScripts.InstallerPostInstallScript();
}
