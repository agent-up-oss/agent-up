using AgentUp.Installers.Features.MacOs.Services;

namespace AgentUp.Packaging.Features.MacOs.Services;

public static class MacOsScriptGenerator
{
    public static string PreInstallScript()
        => MacOsInstallerScripts.PreInstallScript();

    public static string PostInstallScript()
        => MacOsInstallerScripts.PostInstallScript();
}
