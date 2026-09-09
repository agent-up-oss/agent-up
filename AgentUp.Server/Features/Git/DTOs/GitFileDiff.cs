namespace AgentUp.Server.Features.Git.DTOs;

public sealed record GitFileDiff(
    string Path,
    GitChangeStatus Status,
    bool IsBinary,
    string Diff);
