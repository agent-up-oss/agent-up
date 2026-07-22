using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Services;

namespace AgentUp.Installers.Features.Installation.Controllers;

public sealed class InstallerWorkflowController
{
    public bool CanGoBack(InstallerSession session) => Services.InstallerWorkflow.CanGoBack(session);

    public bool CanGoNext(InstallerSession session) => Services.InstallerWorkflow.CanGoNext(session);

    public InstallerSession GoNext(InstallerSession session) => Services.InstallerWorkflow.GoNext(session);

    public InstallerSession GoBack(InstallerSession session) => Services.InstallerWorkflow.GoBack(session);

    public InstallerSession AcceptLicense(InstallerSession session, bool accepted)
        => Services.InstallerWorkflow.AcceptLicense(session, accepted);

    public InstallerSession WithDockerStatus(InstallerSession session, PrerequisiteChecks.Models.DockerStatus status)
        => Services.InstallerWorkflow.WithDockerStatus(session, status);

    public InstallerSession StartInstall(InstallerSession session) => Services.InstallerWorkflow.StartInstall(session);

    public InstallerSession Complete(InstallerSession session, ValidationReport report)
        => Services.InstallerWorkflow.Complete(session, report);
}
