using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Features.Installation.Services;

public sealed class InstallerWorkflowService
{
    public bool CanGoBack(InstallerSession session) => InstallerWorkflow.CanGoBack(session);

    public bool CanGoNext(InstallerSession session) => InstallerWorkflow.CanGoNext(session);

    public InstallerSession GoNext(InstallerSession session) => InstallerWorkflow.GoNext(session);

    public InstallerSession GoBack(InstallerSession session) => InstallerWorkflow.GoBack(session);

    public InstallerSession AcceptLicense(InstallerSession session, bool accepted)
        => InstallerWorkflow.AcceptLicense(session, accepted);

    public InstallerSession WithDockerStatus(InstallerSession session, DockerStatus status)
        => InstallerWorkflow.WithDockerStatus(session, status);

    public InstallerSession WithDockerStatus(InstallerSession session, WorkflowDockerStatus status)
        => InstallerWorkflow.WithDockerStatus(session, new DockerStatus(
            status.Kind switch
            {
                WorkflowDockerStatusKind.NotInstalled => DockerStatusKind.NotInstalled,
                WorkflowDockerStatusKind.DaemonNotRunning => DockerStatusKind.DaemonNotRunning,
                WorkflowDockerStatusKind.Inaccessible => DockerStatusKind.Inaccessible,
                WorkflowDockerStatusKind.UnsupportedVersion => DockerStatusKind.UnsupportedVersion,
                WorkflowDockerStatusKind.Operational => DockerStatusKind.Operational,
                _ => throw new ArgumentOutOfRangeException(nameof(status), status.Kind, "Unsupported Docker status kind.")
            },
            status.Title,
            status.Detail,
            status.Version));

    public InstallerSession StartInstall(InstallerSession session) => InstallerWorkflow.StartInstall(session);

    public InstallerSession Complete(InstallerSession session, ValidationReport report)
        => InstallerWorkflow.Complete(session, report);
}
