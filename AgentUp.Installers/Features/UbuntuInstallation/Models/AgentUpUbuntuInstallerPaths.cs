namespace AgentUp.Installers.Features.UbuntuInstallation.Models;

public sealed partial record UbuntuInstallerPaths
{
    public static UbuntuInstallerPaths SystemDefault()
        => ForProduct(UbuntuInstallerManifest.AgentUp());
}
