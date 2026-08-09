using LocalInstaller.Core.Features.Installation.DTOs;
using LocalInstaller.Core.Features.Installation.Models;
using LocalInstaller.Core.Features.WindowsInstallation.Services;

namespace LocalInstaller.Core.Features.WindowsInstallation.Controllers;

public sealed class WindowsInstallationController
{
    private readonly WindowsInstallationService _service;

    public WindowsInstallationController(WindowsInstallationService service)
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
