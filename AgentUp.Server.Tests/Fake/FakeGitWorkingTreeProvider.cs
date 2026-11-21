using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Interfaces;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeGitWorkingTreeProvider : IGitWorkingTreeProvider
{
    public List<GitChangeEntry> Changes { get; } = [];

    public GitFileDiff? FileDiff { get; set; }

    public string Commit { get; set; } = "0123456789abcdef";

    public string? Failure { get; set; }

    public string Branch { get; set; } = "main";

    public List<string> LocalBranches { get; } = ["main"];

    public List<GitRemoteBranch> RemoteBranches { get; } = [];

    public string? Upstream { get; set; }

    public int Ahead { get; set; }

    public int Behind { get; set; }

    public GitLog Log { get; set; } = new([]);

    public string? CommittedMessage { get; private set; }

    public IReadOnlyList<string> CommittedFiles { get; private set; } = [];

    public IReadOnlyList<string> DiscardedFiles { get; private set; } = [];

    public TaskCompletionSource? HoldDiscard { get; set; }

    public TaskCompletionSource? DiscardStarted { get; set; }

    public string? SwitchedBranch { get; private set; }

    public string? StartedBranch { get; private set; }

    public bool CreatedBranch { get; private set; }

    public string? CheckedOutBranch { get; private set; }

    public string? FetchedRemote { get; private set; }

    public bool? PulledRebase { get; private set; }

    public bool? PushedForceWithLease { get; private set; }

    public bool? PushedSetUpstream { get; private set; }

    public int? LogMax { get; private set; }

    public int? LogSkip { get; private set; }

    public string? LogUntil { get; private set; }

    public string? LastWorktreePath { get; private set; }

    public Task<IReadOnlyList<GitChangeEntry>> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        return Failure is null
            ? Task.FromResult<IReadOnlyList<GitChangeEntry>>(Changes)
            : Task.FromException<IReadOnlyList<GitChangeEntry>>(new InvalidOperationException(Failure));
    }

    public Task<GitHeadState> GetHeadStateAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        return Failure is null
            ? Task.FromResult(new GitHeadState(Branch, LocalBranches, RemoteBranches, Upstream, Ahead, Behind, Commit))
            : Task.FromException<GitHeadState>(new InvalidOperationException(Failure));
    }

    public Task<GitFileDiff?> GetFileDiffAsync(string worktreePath, string filePath, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        return Failure is null
            ? Task.FromResult(FileDiff)
            : Task.FromException<GitFileDiff?>(new InvalidOperationException(Failure));
    }

    public Task<string> CommitAsync(string worktreePath, IReadOnlyList<string> files, string message, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        CommittedFiles = files;
        CommittedMessage = message;
        return Failure is null
            ? Task.FromResult(Commit)
            : Task.FromException<string>(new InvalidOperationException(Failure));
    }

    public async Task DiscardAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        DiscardedFiles = files;
        DiscardStarted?.TrySetResult();
        if (HoldDiscard is not null)
            await HoldDiscard.Task.WaitAsync(cancellationToken);
        if (Failure is not null)
            throw new InvalidOperationException(Failure);
    }

    public GitIdentity? CommitAuthor { get; private set; }

    public string? ResetBranch { get; private set; }

    public Task<string?> CommitAllToBranchAsync(string worktreePath, string branch, string message, GitIdentity author, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        if (Failure is not null)
            return Task.FromException<string?>(new InvalidOperationException(Failure));
        // Like the provider: nothing changed means nothing to commit, and the checkout stays put.
        if (Changes.Count == 0)
            return Task.FromResult<string?>(null);
        Branch = branch;
        CommittedMessage = message;
        CommitAuthor = author;
        CommittedFiles = [.. Changes.Select(change => change.Path)];
        Changes.Clear();
        return Task.FromResult<string?>(Commit);
    }

    public string RangeDiff { get; set; } = string.Empty;

    public Task<string> GetRangeDiffAsync(string worktreePath, string baseRef, string head, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        return Failure is null ? Task.FromResult(RangeDiff) : Task.FromException<string>(new InvalidOperationException(Failure));
    }

    /// <summary>The commit each ref names; a ref that is not here resolves to nothing.</summary>
    public Dictionary<string, string> Refs { get; } = new(StringComparer.Ordinal);

    public Task<string?> ResolveCommitAsync(string worktreePath, string reference, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        if (Failure is not null)
            return Task.FromException<string?>(new InvalidOperationException(Failure));
        return Task.FromResult(reference == "HEAD" && !Refs.ContainsKey("HEAD") ? Commit : Refs.GetValueOrDefault(reference));
    }

    public Task ResetToRemoteBranchAsync(string worktreePath, string branch, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        if (Failure is not null)
            return Task.FromException(new InvalidOperationException(Failure));
        ResetBranch = branch;
        Branch = branch;
        return Task.CompletedTask;
    }

    public Task StartBranchAsync(string worktreePath, string branch, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        StartedBranch = branch;
        if (Failure is null)
            Branch = branch;
        return Failure is null ? Task.CompletedTask : Task.FromException(new InvalidOperationException(Failure));
    }

    public Task SwitchBranchAsync(string worktreePath, string name, bool create, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        SwitchedBranch = name;
        CreatedBranch = create;
        if (Failure is null)
            Branch = name;
        return Failure is null ? Task.CompletedTask : Task.FromException(new InvalidOperationException(Failure));
    }

    public Task CheckoutRemoteAsync(string worktreePath, string name, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        CheckedOutBranch = name;
        if (Failure is null)
            Branch = name.Contains('/') ? name[(name.IndexOf('/') + 1)..] : name;
        return Failure is null ? Task.CompletedTask : Task.FromException(new InvalidOperationException(Failure));
    }

    public Task FetchAsync(string worktreePath, string? remote, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        FetchedRemote = remote;
        return Failure is null ? Task.CompletedTask : Task.FromException(new InvalidOperationException(Failure));
    }

    public Task PullAsync(string worktreePath, bool rebase, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        PulledRebase = rebase;
        return Failure is null ? Task.CompletedTask : Task.FromException(new InvalidOperationException(Failure));
    }

    public Task PushAsync(string worktreePath, bool forceWithLease, bool setUpstream, CancellationToken cancellationToken = default)
    {
        LastWorktreePath = worktreePath;
        PushedForceWithLease = forceWithLease;
        PushedSetUpstream = setUpstream;
        return Failure is null ? Task.CompletedTask : Task.FromException(new InvalidOperationException(Failure));
    }

    public Task<GitLog> GetLogAsync(
        string worktreePath,
        int? max,
        CancellationToken cancellationToken = default,
        int? skip = null,
        string? until = null)
    {
        LastWorktreePath = worktreePath;
        LogMax = max;
        LogSkip = skip;
        LogUntil = until;
        return Failure is null
            ? Task.FromResult(Log)
            : Task.FromException<GitLog>(new InvalidOperationException(Failure));
    }
}
