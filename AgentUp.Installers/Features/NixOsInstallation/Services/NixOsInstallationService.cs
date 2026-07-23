using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Features.NixOsInstallation.Services;

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
