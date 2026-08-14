using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.UbuntuInstallation.Services;

namespace LocalInstaller.Core.Features.UbuntuInstallation.Controllers;

public sealed class UbuntuInstallationController
{
    private readonly UbuntuInstallationService _service;

    public UbuntuInstallationController(UbuntuInstallationService service)
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
