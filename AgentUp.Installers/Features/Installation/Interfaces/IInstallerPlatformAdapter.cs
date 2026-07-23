using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Features.Installation.Interfaces;

public interface IInstallerPlatformAdapter
{
    string PlatformName { get; }

    bool SupportsInstallActions { get; }

    Task<DockerStatus> CheckDockerAsync(CancellationToken cancellationToken = default);

    Task<InstallerComponentStatus> GetComponentStatusAsync(
        ProductComponent component,
        InstallerSession session,
        CancellationToken cancellationToken = default);

    IReadOnlyList<InstallOperation> PlanComponentAction(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session);

    IAsyncEnumerable<InstallProgress> ExecuteComponentActionAsync(
        ProductComponent component,
        InstallerComponentAction action,
        InstallerSession session,
        CancellationToken cancellationToken = default);

    IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session);

    IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default);

    Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default);
}
