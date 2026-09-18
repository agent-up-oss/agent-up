namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitLogCommit(
    string Id,
    string ShortId,
    IReadOnlyList<string> Parents,
    string Subject,
    string Author,
    string Timestamp,
    IReadOnlyList<string> Refs);
