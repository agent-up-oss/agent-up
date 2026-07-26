namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitsStagingResult(
    string Slice,
    string Message,
    IReadOnlyList<string> StagedFiles,
    int RemainingCount);
