using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;

namespace AgentUp.Installers.Features.Installation.Controllers;

public sealed class InstallerWorkflowController
{
    private readonly InstallerWorkflowService _service;

    public InstallerWorkflowController(InstallerWorkflowService service)
    {
        _service = service;
    }

    public bool CanGoBack(InstallerSession session) => _service.CanGoBack(session);

    public bool CanGoNext(InstallerSession session) => _service.CanGoNext(session);

    public InstallerSession GoNext(InstallerSession session) => _service.GoNext(session);

    public InstallerSession GoBack(InstallerSession session) => _service.GoBack(session);

    public InstallerSession AcceptLicense(InstallerSession session, bool accepted)
        => _service.AcceptLicense(session, accepted);

    public InstallerSession WithDockerStatus(InstallerSession session, WorkflowDockerStatus status)
        => _service.WithDockerStatus(session, status);

    public InstallerSession StartInstall(InstallerSession session) => _service.StartInstall(session);

    public InstallerSession Complete(InstallerSession session, ValidationReport report)
        => _service.Complete(session, report);
}
