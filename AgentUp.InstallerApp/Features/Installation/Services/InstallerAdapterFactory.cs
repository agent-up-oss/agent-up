using AgentUp.Installers.Features.Execution;

namespace AgentUp.InstallerApp.Features.Installation.Services;

internal static class InstallerAdapterFactory
{
    public static IInstallerPlatformAdapter Create()
        => InstallerPlatformAdapterFactory.Create();
}
