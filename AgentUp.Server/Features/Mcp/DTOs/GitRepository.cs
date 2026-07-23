namespace AgentUp.Server.Features.Mcp.DTOs;

internal sealed record GitRepository(
    string WorktreeRoot,
    string GitDirectory,
    string CommonDirectory,
    string RepositoryPath);
