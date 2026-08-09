using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.MacOsInstallation.Services;

namespace LocalInstaller.Core.Features.MacOsInstallation.Controllers;

public sealed class MacOsInstallationController
{
    private readonly MacOsInstallationService _service;

    public MacOsInstallationController(MacOsInstallationService service)
    {
        _service = service;
    }

    public IReadOnlyList<InstallOperation> PlanInstall(InstallerSession session)
        => _service.PlanInstall(session);

    public IAsyncEnumerable<InstallProgress> ExecuteInstallAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => _service.ExecuteInstallAsync(session, cancellationToken);

    public async Task<ValidationReport> ValidateInstalledStateAsync(
        InstallerSession session,
        CancellationToken cancellationToken = default)
        => await _service.ValidateInstalledStateAsync(session, cancellationToken);
}
