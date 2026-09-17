namespace AgentUp.Server.Features.Commits.Models;

public sealed record CommitsQueue(
    int Version,
    IReadOnlyList<CommitEntry> Commits,
    CommitEditSession? ActiveSession = null,
    IReadOnlyList<ArchivedCommitEntry>? Archive = null,
    string? QueueId = null,
    string? BaseCommit = null,
    string? TipCommit = null,
    string? QueueWorktreePath = null,
    long Generation = 0)
{
    public IReadOnlyList<ArchivedCommitEntry> Archived => Archive ?? [];

    public static CommitsQueue Empty() => new(3, []);
}
