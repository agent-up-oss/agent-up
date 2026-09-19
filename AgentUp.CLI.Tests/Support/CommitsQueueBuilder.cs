using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Tests.Support;

/// <summary>
/// Builds a <see cref="CommitsQueue"/> at the current queue version, so a test states only
/// the entries and session state it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="CommitsQueue"/> positionally. The version
/// number in particular is a detail of the persisted shape rather than of any test, and it
/// lives here so a bump changes one file.
/// </remarks>
internal sealed class CommitsQueueBuilder
{
    /// <summary>The current on-disk queue version, matching <see cref="CommitsQueue.Empty"/>.</summary>
    public const int CurrentVersion = 2;

    private int _version = CurrentVersion;
    private readonly List<CommitEntry> _commits = [];
    private CommitEditSession? _activeSession;
    private List<ArchivedCommitEntry>? _archive;

    public CommitsQueueBuilder AtVersion(int version)
    {
        _version = version;
        return this;
    }

    public CommitsQueueBuilder With(CommitEntryBuilder commit)
    {
        _commits.Add(commit.Build());
        return this;
    }

    public CommitsQueueBuilder With(CommitEntry commit)
    {
        _commits.Add(commit);
        return this;
    }

    public CommitsQueueBuilder With(IEnumerable<CommitEntry> commits)
    {
        _commits.AddRange(commits);
        return this;
    }

    public CommitsQueueBuilder WithActiveSession(CommitEditSession? session)
    {
        _activeSession = session;
        return this;
    }

    public CommitsQueueBuilder WithArchived(params ArchivedCommitEntry[] archived)
    {
        _archive = [.. archived];
        return this;
    }

    public CommitsQueue Build() => new(_version, _commits, _activeSession, _archive);
}
