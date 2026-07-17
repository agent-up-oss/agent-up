using AgentUp.CLI.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Features.Workspaces.DTOs;

public record WorkspaceDto(
    string Id,
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit,
    string State)
{
    public IReadOnlyList<ApplicationDefinition> Applications { get; init; } = [];
}
