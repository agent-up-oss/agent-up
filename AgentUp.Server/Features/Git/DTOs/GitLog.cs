namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitLog(IReadOnlyList<GitLogCommit> Commits, bool HasMore = false);
