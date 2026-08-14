using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.NixOsInstallation.Services;

namespace LocalInstaller.Core.Features.NixOsInstallation.Controllers;

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
