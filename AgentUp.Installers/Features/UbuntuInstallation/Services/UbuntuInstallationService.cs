using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;

namespace AgentUp.Installers.Features.UbuntuInstallation.Services;

public sealed class UbuntuInstallationService
{
    private readonly IInstallerPlatformAdapter _adapter;

    public UbuntuInstallationService(IInstallerPlatformAdapter adapter)
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
