namespace AgentUp.AUDebug.Features.Host.DTOs;

public sealed record HostSessionDto(
    int SupervisorPid,
    string RepositoryRoot,
    string SessionDirectory,
    IReadOnlyList<HostedProcessDto> Processes);
