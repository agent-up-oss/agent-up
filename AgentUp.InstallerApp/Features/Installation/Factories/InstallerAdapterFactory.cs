using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Execution.Providers;

namespace AgentUp.InstallerApp.Features.Installation.Providers;

internal static class InstallerAdapterFactory
{
    public static IInstallerPlatformAdapter Create()
        => InstallerPlatformAdapterFactory.Create();
}
