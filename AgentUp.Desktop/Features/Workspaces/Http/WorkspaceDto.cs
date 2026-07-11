namespace AgentUp.Desktop.Features.Workspaces.Http;

public sealed record WorkspaceDto(
    string Id,
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit,
    string State);
