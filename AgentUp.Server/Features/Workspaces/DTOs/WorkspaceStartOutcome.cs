namespace AgentUp.Server.Features.Workspaces.DTOs;

public sealed record WorkspaceStartOutcome(
    WorkspaceState State,
    IReadOnlyList<WorkspaceApplicationFailure> FailedApplications);
