using AgentUp.Desktop.Features.Applications.DTOs;

namespace AgentUp.Desktop.Features.Workspaces.DTOs;

public sealed record WorkspaceDto(
    string Id,
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit,
    string State)
{
    public List<ApplicationDto> Applications { get; init; } = [];
}
