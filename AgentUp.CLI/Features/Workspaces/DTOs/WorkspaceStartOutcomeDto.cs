namespace AgentUp.CLI.Features.Workspaces.DTOs;

public sealed record WorkspaceStartOutcomeDto(
    string State,
    IReadOnlyList<WorkspaceApplicationFailureDto> FailedApplications);

public sealed record WorkspaceApplicationFailureDto(string Name, string Reason);
