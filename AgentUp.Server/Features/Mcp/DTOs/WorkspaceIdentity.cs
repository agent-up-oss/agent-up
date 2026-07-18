namespace AgentUp.Server.Features.Mcp.DTOs;

public sealed record WorkspaceIdentity(
    string RepositoryPath,
    string Branch,
    string Commit);
