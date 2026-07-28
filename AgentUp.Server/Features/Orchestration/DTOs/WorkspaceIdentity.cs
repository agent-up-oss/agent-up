namespace AgentUp.Server.Features.Orchestration.DTOs;

public sealed record WorkspaceIdentity(
    string RepositoryPath,
    string Branch,
    string Commit);
