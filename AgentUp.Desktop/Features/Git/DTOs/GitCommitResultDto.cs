namespace AgentUp.Desktop.Features.Git.DTOs;

public sealed record GitCommitResultDto(
    bool Found,
    bool Succeeded,
    string? Commit,
    string? Error);
