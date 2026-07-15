using AgentUp.Installers.Features.Flow;
using AgentUp.Installers.Features.Prerequisites;
using AgentUp.Installers.Features.Validation;

namespace AgentUp.Installers.Features.Execution;

public interface IInstallerPlatformAdapter
{
    string PlatformName { get; }

    Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session);

    IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default);

    Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default);
}
