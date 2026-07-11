namespace AgentUp.Server.Features.Workspaces.DTOs;

public class Workspace
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string RepositoryPath { get; init; }
    public required string WorktreePath { get; init; }
    public required string Branch { get; init; }
    public required string Commit { get; init; }
    public WorkspaceState State { get; set; } = WorkspaceState.Stopped;
    public IReadOnlyList<ApplicationInstance> Applications { get; init; } = [];
}
