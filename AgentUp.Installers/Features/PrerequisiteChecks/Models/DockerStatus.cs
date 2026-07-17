namespace AgentUp.Installers.Features.PrerequisiteChecks.Models;

public enum DockerStatusKind
{
    NotInstalled,
    DaemonNotRunning,
    Inaccessible,
    UnsupportedVersion,
    Operational
}

public sealed record DockerStatus(
    DockerStatusKind Kind,
    string Title,
    string Detail,
    Version? Version = null)
{
    public bool CanContinue => Kind == DockerStatusKind.Operational;
}
