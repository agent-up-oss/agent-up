namespace AgentUp.CLI.Http;

public record RegisterWorkspaceRequest(
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit);
