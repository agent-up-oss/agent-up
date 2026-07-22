using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.UbuntuInstallation.Services;

namespace AgentUp.Installers.Features.UbuntuInstallation.Controllers;

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
