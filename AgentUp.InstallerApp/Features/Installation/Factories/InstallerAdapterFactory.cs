using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Interfaces;

namespace AgentUp.InstallerApp.Features.Installation.Factories;

internal static class InstallerAdapterFactory
{
    public static IInstallerPlatformAdapter Create()
        => InstallerPlatformAdapterFactory.Create();
}
