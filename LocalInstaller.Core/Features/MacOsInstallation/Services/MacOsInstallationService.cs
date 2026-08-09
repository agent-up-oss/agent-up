using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Interfaces;
using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.Core.Features.MacOsInstallation.Services;

public sealed class MacOsInstallationService
{
    private readonly IInstallerPlatformAdapter _adapter;

    public MacOsInstallationService(IInstallerPlatformAdapter adapter)
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
