using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Services;

namespace AgentUp.Packaging.Features.MacOsPackages.Services;

public static class MacOsScriptGenerator
{
    public static string PreInstallScript()
        => MacOsInstallerScripts.PreInstallScript();

    public static string PostInstallScript()
        => MacOsInstallerScripts.PostInstallScript();
}
