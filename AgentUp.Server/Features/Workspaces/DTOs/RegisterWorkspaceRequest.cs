namespace AgentUp.Server.Features.Workspaces.DTOs;

public record RegisterWorkspaceRequest(
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit)
{
    public IReadOnlyList<ApplicationDefinition> Applications { get; init; } = [];
}
