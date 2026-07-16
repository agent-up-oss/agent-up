namespace AgentUp.Installers.Features.Installation.Services;

public enum UninstallMode
{
    ApplicationOnly,
    ApplicationAndConfiguration,
    ApplicationConfigurationAndLocalData
}

public sealed record UninstallPlan(
    bool RemoveBinaries,
    bool RemoveService,
    bool RemoveLauncherEntries,
    bool RemovePathEntries,
    bool RemovePackageMetadata,
    bool RemoveConfiguration,
    bool RemoveLocalData);

public static class UninstallPlanner
{
    public static UninstallPlan Create(UninstallMode mode)
        => new(
            RemoveBinaries: true,
            RemoveService: true,
            RemoveLauncherEntries: true,
            RemovePathEntries: true,
            RemovePackageMetadata: true,
            RemoveConfiguration: mode is UninstallMode.ApplicationAndConfiguration or UninstallMode.ApplicationConfigurationAndLocalData,
            RemoveLocalData: mode is UninstallMode.ApplicationConfigurationAndLocalData);
}
