using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;

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
