namespace AgentUp.Desktop.Features.Git.DTOs;

public sealed record GitFileDiffDto(
    string Path,
    string Status,
    bool IsBinary,
    string Diff);
