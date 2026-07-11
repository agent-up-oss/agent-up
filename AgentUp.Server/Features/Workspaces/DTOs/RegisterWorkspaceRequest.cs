namespace AgentUp.Server.Features.Workspaces.DTOs;

// Input DTO — definition only, no runtime state
public record ApplicationDefinition(
    string Name,
    string Command,
    string? Path,
    string? PortVariable);

public record RegisterWorkspaceRequest(
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit)
{
    public IReadOnlyList<ApplicationDefinition> Applications { get; init; } = [];
}
