using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Features.Installation.Interfaces;

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
