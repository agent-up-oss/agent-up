namespace AgentUp.Server.Features.Orchestration.DTOs;

internal sealed record GitRepository(
    string WorktreeRoot,
    string GitDirectory,
    string CommonDirectory,
    string RepositoryPath);
