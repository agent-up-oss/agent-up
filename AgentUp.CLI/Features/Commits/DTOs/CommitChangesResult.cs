namespace AgentUp.CLI.Features.Commits.DTOs;

public sealed record CommitChangesResult(
    IReadOnlyList<string> ModifiedFiles,
    IReadOnlyList<string> StagedFiles,
    IReadOnlyList<string> UntrackedFiles,
    IReadOnlyList<string> QueuedFiles,
    IReadOnlyList<string> UnassignedFiles);
