namespace AgentUp.Server.Features.Commits.Models;

public sealed record CommitsQueue(
    int Version,
    IReadOnlyList<CommitEntry> Commits,
    CommitEditSession? ActiveSession = null,
    IReadOnlyList<ArchivedCommitEntry>? Archive = null)
{
    public IReadOnlyList<ArchivedCommitEntry> Archived => Archive ?? [];

    public static CommitsQueue Empty() => new(2, []);
}
