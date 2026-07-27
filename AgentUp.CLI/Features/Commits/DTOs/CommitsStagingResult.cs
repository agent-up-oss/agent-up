namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitsStagingResult(
    string Slice,
    string Message,
    IReadOnlyList<string> StagedFiles,
    int RemainingCount,
    string? BlockedReason = null)
{
    public bool IsBlocked => BlockedReason is not null;

    public static CommitsStagingResult Blocked(string reason)
        => new(string.Empty, string.Empty, [], 0, reason);
}
