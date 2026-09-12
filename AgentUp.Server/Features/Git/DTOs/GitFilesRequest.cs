namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitFilesRequest(IReadOnlyList<string>? Files);

public sealed record GitBranchRequest(string? Name, bool Create);
