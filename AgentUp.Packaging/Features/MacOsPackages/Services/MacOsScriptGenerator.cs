using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Services;

namespace AgentUp.Packaging.Features.MacOsPackages.Services;

public static class MacOsScriptGenerator
{
    public static string PreInstallScript()
        => MacOsInstallerScripts.PreInstallScript();

    public static string PostInstallScript()
        => MacOsInstallerScripts.PostInstallScript();
}
