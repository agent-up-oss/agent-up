namespace AgentUp.CLI.Features.Workspaces.Models;

public sealed record WorkspaceIdentity(string RepositoryPath, string Branch, string Commit);
