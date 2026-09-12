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
            head.LocalBranches);
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
                        request.Files ?? [],
                        request.Message ?? string.Empty,
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
                    await _git.DiscardAsync(workspace.WorktreePath, request.Files ?? [], cancellationToken);
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

    private async Task<T> MutateAsync<T>(
        string workspaceId,
        Func<Workspace, Task<T>> action,
        Func<T> notFound,
        Func<string, T> failed,
        CancellationToken cancellationToken)
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
            if (_prompts.IsPromptRunning(workspaceId))
                return failed(PromptRunningError);
            return await action(workspace);
        }
        finally
        {
            gate.Release();
        }
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
            return new GitHeadState(workspace.Branch, []);
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
