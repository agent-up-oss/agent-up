namespace AgentUp.Desktop.Features.Git.DTOs;

public sealed record GitCommitRequestDto(IReadOnlyList<string> Files, string Message);
