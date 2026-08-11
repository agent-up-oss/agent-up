using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Interfaces;
using LocalInstaller.Core.Features.Installation.Models;

namespace LocalInstaller.Core.Features.NixOsInstallation.Services;

public sealed class NixOsInstallationService
{
    private readonly IInstallerPlatformAdapter _adapter;

    public NixOsInstallationService(IInstallerPlatformAdapter adapter)
    {
        _adapter = adapter;
    }

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => await _adapter.ValidateInstalledStateAsync(session, cancellationToken);
}
