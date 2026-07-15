namespace AgentUp.Packaging.Features.MacOs;

public static class MacOsScriptGenerator
{
    public static string PreInstallScript()
        => AgentUp.Installers.Features.MacOs.MacOsInstallerScripts.PreInstallScript();

    public static string PostInstallScript()
        => AgentUp.Installers.Features.MacOs.MacOsInstallerScripts.PostInstallScript();
}
