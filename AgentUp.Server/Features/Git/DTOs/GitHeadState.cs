namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitHeadState(string Branch, IReadOnlyList<string> LocalBranches);
