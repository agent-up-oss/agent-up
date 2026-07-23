using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.WindowsInstallation.Services;

public sealed class WindowsInstallationService
{
    private readonly IInstallerPlatformAdapter _adapter;

    public WindowsInstallationService(IInstallerPlatformAdapter adapter)
    {
        _adapter = adapter;
    }

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        => _adapter.PlanInstall(session);

    public IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => _adapter.ExecuteInstallAsync(session, cancellationToken);

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => await _adapter.ValidateInstalledStateAsync(session, cancellationToken);
}
