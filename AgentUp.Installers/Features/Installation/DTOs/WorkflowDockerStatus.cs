namespace AgentUp.Installers.Features.Installation.DTOs;

public enum WorkflowDockerStatusKind
{
    NotInstalled,
    DaemonNotRunning,
    Inaccessible,
    UnsupportedVersion,
    Operational
}

public sealed record WorkflowDockerStatus(
    WorkflowDockerStatusKind Kind,
    string Title,
    string Detail,
    Version? Version = null);
