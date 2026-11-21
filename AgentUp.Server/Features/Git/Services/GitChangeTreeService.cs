using System.Collections.Concurrent;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Git.Services;

public sealed class GitChangeTreeService
{
    private const string PromptRunningError = "Cannot change files while the workspace agent is processing a prompt.";
    private readonly WorkspaceQueryController _workspaces;
    private readonly IGitWorkingTreeProvider _git;
    private readonly IWorkspacePromptGuard _prompts;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _mutations = new();

    public GitChangeTreeService(
        WorkspaceQueryController workspaces,
        IGitWorkingTreeProvider git,
        IWorkspacePromptGuard prompts)
    {
        _workspaces = workspaces;
        _git = git;
        _prompts = prompts;
    }

    public async Task<GitChangeTree?> GetChangesAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var changes = await ReadChangesAsync(workspace, cancellationToken);
        var head = await ReadHeadAsync(workspace, cancellationToken);
        return new GitChangeTree(
            workspace.Id,
            head.Branch.Length > 0 ? head.Branch : workspace.Branch,
            changes.Count,
            BuildTree(changes),
            head.LocalBranches,
            head.RemoteBranches,
            head.Upstream,
            head.Ahead,
            head.Behind,
            head.Commit.Length > 0 ? head.Commit : workspace.Commit);
    }

    public async Task<GitHeadState?> GetHeadAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        return await ReadHeadAsync(workspace, cancellationToken);
    }

    public async Task<GitFileDiff?> GetFileDiffAsync(
        string workspaceId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        try
        {
            return await _git.GetFileDiffAsync(workspace.WorktreePath, filePath, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    public Task<GitCommitResult> CommitAsync(
        string workspaceId,
        GitCommitRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            workspaceId,
            async workspace =>
            {
                try
                {
                    var commit = await _git.CommitAsync(
                        workspace.WorktreePath,
                        request?.Files ?? [],
                        request?.Message ?? string.Empty,
                        cancellationToken);
                    return GitCommitResult.Success(commit);
                }
                catch (InvalidOperationException ex)
                {
                    return GitCommitResult.Failed(ex.Message);
                }
            },
            GitCommitResult.NotFound,
            GitCommitResult.Failed,
            cancellationToken);

    /// <summary>The commit a ref names in the workspace's repository, or null when it names none.</summary>
    public async Task<string?> ResolveCommitAsync(string workspaceId, string reference, CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;
        try
        {
            return await _git.ResolveCommitAsync(workspace.WorktreePath, reference, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>What a branch adds since it left its base, or null when either ref is unknown.</summary>
    public async Task<string?> GetRangeDiffAsync(string workspaceId, string baseRef, string head, CancellationToken cancellationToken = default)
    {
        var workspace = _workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;
        try
        {
            return await _git.GetRangeDiffAsync(workspace.WorktreePath, baseRef, head, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>Commits everything in the workspace onto a task branch. A null commit means nothing changed.</summary>
    public Task<GitCommitResult> CommitAllToBranchAsync(
        string workspaceId,
        string branch,
        string message,
        GitIdentity author,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            workspaceId,
            async workspace =>
            {
                try
                {
                    var commit = await _git.CommitAllToBranchAsync(workspace.WorktreePath, branch, message, author, cancellationToken);
                    await RefreshIdentityAsync(workspace, cancellationToken);
                    return commit is null ? GitCommitResult.Unchanged() : GitCommitResult.Success(commit);
                }
                catch (InvalidOperationException ex)
                {
                    return GitCommitResult.Failed(ex.Message);
                }
            },
            GitCommitResult.NotFound,
            GitCommitResult.Failed,
            cancellationToken,
            forTheAgent: true);

    /// <summary>Starts a task branch at the commit now checked out, so the work has a home before it has a commit.</summary>
    public Task<GitMutationResult> StartBranchAsync(
        string workspaceId,
        string branch,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            workspaceId,
            async workspace =>
            {
                try
                {
                    await _git.StartBranchAsync(workspace.WorktreePath, branch, cancellationToken);
                    await RefreshIdentityAsync(workspace, cancellationToken);
                    return GitMutationResult.Success();
                }
                catch (InvalidOperationException ex)
                {
                    return GitMutationResult.Failed(ex.Message);
                }
            },
            GitMutationResult.NotFound,
            GitMutationResult.Failed,
            cancellationToken,
            forTheAgent: true);

    /// <summary>Checks out the fetched remote branch exactly, discarding nothing but the old local copy of it.</summary>
    public Task<GitMutationResult> ResetToRemoteBranchAsync(
        string workspaceId,
        string branch,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            workspaceId,
            async workspace =>
            {
                try
                {
                    await _git.ResetToRemoteBranchAsync(workspace.WorktreePath, branch, cancellationToken);
                    await RefreshIdentityAsync(workspace, cancellationToken);
                    return GitMutationResult.Success();
                }
                catch (InvalidOperationException ex)
                {
                    return GitMutationResult.Failed(ex.Message);
                }
            },
            GitMutationResult.NotFound,
            GitMutationResult.Failed,
            cancellationToken,
            forTheAgent: true);

    public Task<GitMutationResult> DiscardAsync(
        string workspaceId,
        GitFilesRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            workspaceId,
            async workspace =>
            {
                try
                {
                    await _git.DiscardAsync(workspace.WorktreePath, request?.Files ?? [], cancellationToken);
                    return GitMutationResult.Success();
                }
                catch (InvalidOperationException ex)
                {
                    return GitMutationResult.Failed(ex.Message);
                }
            },
            GitMutationResult.NotFound,
            GitMutationResult.Failed,
            cancellationToken);

    public Task<GitMutationResult> SwitchBranchAsync(
        string workspaceId,
        GitBranchRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            workspaceId,
            async workspace =>
            {
                try
                {
                    await _git.SwitchBranchAsync(workspace.WorktreePath, request.Name ?? string.Empty, request.Create, cancellationToken);
                    await RefreshIdentityAsync(workspace, cancellationToken);
                    return GitMutationResult.Success();
                }
                catch (InvalidOperationException ex)
                {
                    return GitMutationResult.Failed(ex.Message);
                }
            },
            GitMutationResult.NotFound,
            GitMutationResult.Failed,
            cancellationToken);

    public Task<GitMutationResult> CheckoutRemoteAsync(
        string workspaceId,
        GitCheckoutRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            workspaceId,
            async workspace =>
            {
                try
                {
                    await _git.CheckoutRemoteAsync(workspace.WorktreePath, request.Name ?? string.Empty, cancellationToken);
                    await RefreshIdentityAsync(workspace, cancellationToken);
                    return GitMutationResult.Success();
                }
                catch (InvalidOperationException ex)
                {
                    return GitMutationResult.Failed(ex.Message);
                }
            },
            GitMutationResult.NotFound,
            GitMutationResult.Failed,
            cancellationToken);

    public Task<GitSyncResult> FetchAsync(
        string workspaceId,
        GitFetchRequest request,
        CancellationToken cancellationToken = default) =>
        MutateSyncAsync(
            workspaceId,
            workspace => _git.FetchAsync(workspace.WorktreePath, request.Remote, cancellationToken),
            refreshIdentity: false,
            cancellationToken);

    public Task<GitSyncResult> PullAsync(
        string workspaceId,
        GitPullRequest request,
        CancellationToken cancellationToken = default) =>
        MutateSyncAsync(
            workspaceId,
            workspace => _git.PullAsync(workspace.WorktreePath, request.Rebase, cancellationToken),
            refreshIdentity: true,
            cancellationToken);

    public Task<GitSyncResult> PushAsync(
        string workspaceId,
        GitPushRequest request,
        CancellationToken cancellationToken = default) =>
        MutateSyncAsync(
            workspaceId,
            workspace => _git.PushAsync(workspace.WorktreePath, request.ForceWithLease, request.SetUpstream, cancellationToken),
            refreshIdentity: false,
            cancellationToken);

    public async Task<GitLog?> GetLogAsync(
        string workspaceId,
        int? max,
        CancellationToken cancellationToken = default,
        int? skip = null,
        string? until = null)
    {
        var workspace = _workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        try
        {
            return await _git.GetLogAsync(workspace.WorktreePath, max, cancellationToken, skip, until);
        }
        catch (InvalidOperationException)
        {
            return new GitLog([]);
        }
    }

    private async Task<T> MutateAsync<T>(
        string workspaceId,
        Func<Workspace, Task<T>> action,
        Func<T> notFound,
        Func<string, T> failed,
        CancellationToken cancellationToken,
        bool forTheAgent = false)
    {
        var workspace = _workspaces.GetById(workspaceId);
        if (workspace is null)
            return notFound();

        var gate = _mutations.GetOrAdd(workspace.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            workspace = _workspaces.GetById(workspaceId);
            if (workspace is null)
                return notFound();
            // A person changing files under a running agent would race it. The Server's own task
            // branch writes are made for that agent, often from inside its turn when it calls
            // submit_work, so they are the one change a running prompt does not block.
            if (!forTheAgent && _prompts.IsPromptRunning(workspaceId))
                return failed(PromptRunningError);
            return await action(workspace);
        }
        finally
        {
            gate.Release();
        }
    }

    private Task<GitSyncResult> MutateSyncAsync(
        string workspaceId,
        Func<Workspace, Task> action,
        bool refreshIdentity,
        CancellationToken cancellationToken) =>
        MutateAsync(
            workspaceId,
            async workspace =>
            {
                try
                {
                    await action(workspace);
                    if (refreshIdentity)
                        await RefreshIdentityAsync(workspace, cancellationToken);
                    return GitSyncResult.Success(await ReadHeadAsync(workspace, cancellationToken));
                }
                catch (InvalidOperationException ex)
                {
                    return GitSyncResult.Failed(ex.Message);
                }
            },
            GitSyncResult.NotFound,
            GitSyncResult.Failed,
            cancellationToken);

    private async Task RefreshIdentityAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        var head = await ReadHeadAsync(workspace, cancellationToken);
        var branch = head.Branch.Length > 0 ? head.Branch : workspace.Branch;
        var commit = head.Commit.Length > 0 ? head.Commit : workspace.Commit;
        await _workspaces.UpdateGitIdentityAsync(workspace.Id, branch, commit);
    }

    private async Task<IReadOnlyList<GitChangeEntry>> ReadChangesAsync(
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _git.GetChangesAsync(workspace.WorktreePath, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return [];
        }
    }

    private async Task<GitHeadState> ReadHeadAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        try
        {
            return await _git.GetHeadStateAsync(workspace.WorktreePath, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return GitHeadState.Empty(workspace.Branch, workspace.Commit);
        }
    }

    private static GitChangeDirectory BuildTree(IReadOnlyList<GitChangeEntry> changes)
        => BuildDirectory(
            name: string.Empty,
            path: string.Empty,
            changes.Select(change => (Segments: SplitPath(change.Path), Change: change)).ToList(),
            depth: 0);

    private static GitChangeDirectory BuildDirectory(
        string name,
        string path,
        IReadOnlyList<(string[] Segments, GitChangeEntry Change)> entries,
        int depth)
    {
        var files = entries
            .Where(entry => entry.Segments.Length == depth + 1)
            .Select(entry => new GitChangeFile(entry.Segments[depth], entry.Change.Path, entry.Change.Status))
            .OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var directories = entries
            .Where(entry => entry.Segments.Length > depth + 1)
            .GroupBy(entry => entry.Segments[depth], StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildDirectory(
                group.Key,
                Join(path, group.Key),
                group.ToList(),
                depth + 1))
            .ToList();

        return new GitChangeDirectory(name, path, directories, files);
    }

    private static string Join(string parent, string name)
        => parent.Length == 0 ? name : $"{parent}/{name}";

    private static string[] SplitPath(string path)
        => path.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
