using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.NixOsInstallation.Services;

namespace AgentUp.Installers.Features.NixOsInstallation.Controllers;

public sealed class NixOsInstallationController
{
    private readonly NixOsInstallationService _service;

    public NixOsInstallationController(NixOsInstallationService service)
    {
        _service = service;
    }

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => await _service.ValidateInstalledStateAsync(session, cancellationToken);
}
