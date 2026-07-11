namespace AgentUp.CLI.Http;

public record WorkspaceDto(
    string Id,
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit,
    string State);
