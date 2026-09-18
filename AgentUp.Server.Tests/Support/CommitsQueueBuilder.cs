using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Tests.Support;

/// <summary>
/// Builds a <see cref="CommitsQueue"/> at the current queue version, so a test states only
/// the entries and stack state it is actually about.
/// </summary>
/// <remarks>
/// This exists so no test constructs <see cref="CommitsQueue"/> positionally. The version
/// number in particular is a detail of the persisted shape rather than of any test, and it
/// lives here so a bump changes one file.
/// </remarks>
internal sealed class CommitsQueueBuilder
{
    /// <summary>The current on-disk queue version, matching <see cref="CommitsQueue.Empty"/>.</summary>
    public const int CurrentVersion = 3;

    private int _version = CurrentVersion;
    private readonly List<CommitEntry> _commits = [];
    private CommitEditSession? _activeSession;
    private List<ArchivedCommitEntry>? _archive;
    private string? _queueId;
    private string? _baseCommit;
    private string? _tipCommit;
    private string? _queueWorktreePath;
    private long _generation;

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
        _archive ??= [];
        _archive.AddRange(archived);
        return this;
    }

    public CommitsQueueBuilder WithQueueId(string? queueId)
    {
        _queueId = queueId;
        return this;
    }

    public CommitsQueueBuilder OnStack(string? baseCommit, string? tipCommit)
    {
        _baseCommit = baseCommit;
        _tipCommit = tipCommit;
        return this;
    }

    public CommitsQueueBuilder WithBaseCommit(string? baseCommit)
    {
        _baseCommit = baseCommit;
        return this;
    }

    public CommitsQueueBuilder WithTipCommit(string? tipCommit)
    {
        _tipCommit = tipCommit;
        return this;
    }

    public CommitsQueueBuilder InWorktree(string? queueWorktreePath)
    {
        _queueWorktreePath = queueWorktreePath;
        return this;
    }

    public CommitsQueueBuilder AtGeneration(long generation)
    {
        _generation = generation;
        return this;
    }

    public CommitsQueue Build()
        => new(
            _version,
            _commits,
            _activeSession,
            _archive,
            _queueId,
            _baseCommit,
            _tipCommit,
            _queueWorktreePath,
            _generation);
}
