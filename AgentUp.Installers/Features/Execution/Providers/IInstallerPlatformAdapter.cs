using AgentUp.Installers.Features.Execution.Models;
using AgentUp.Installers.Features.Flow.Models;
using AgentUp.Installers.Features.Prerequisites.Services;
using AgentUp.Installers.Features.Validation.Models;

namespace AgentUp.Installers.Features.Execution.Providers;

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
