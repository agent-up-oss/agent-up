using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Shared.Interfaces;

namespace AgentUp.Server.Features.Commits.Services;

/// <summary>
/// The MCP-facing commit queue. Every tool names its target either by registered workspace
/// id or by absolute worktree path, so a caller that does not share the Server's filesystem
/// can still queue commits.
/// </summary>
public sealed class CommitQueueMcpService(CommitsController commits, WorkspaceTargetController workspaces)
{
    public async Task<McpToolResult> EnqueueCommit(
        string slice,
        string message,
        IReadOnlyList<string> files,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);
        if (string.IsNullOrWhiteSpace(slice))
            return new McpToolResult(false, "slice is required.");
        if (string.IsNullOrWhiteSpace(message))
            return new McpToolResult(false, "message is required.");
        if (files.Count == 0)
            return new McpToolResult(false, "At least one file is required.");

        return await EnqueueAsync(target.WorktreePath!, new EnqueueRequest(slice, message, files), cancellationToken);
    }

    public async Task<McpToolResult> EnqueueReviewFixCommit(
        string reviewIssueId,
        string slice,
        string message,
        IReadOnlyList<string> files,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);
        if (string.IsNullOrWhiteSpace(reviewIssueId))
            return new McpToolResult(false, "reviewIssueId is required.");
        if (string.IsNullOrWhiteSpace(slice))
            return new McpToolResult(false, "slice is required.");
        if (string.IsNullOrWhiteSpace(message))
            return new McpToolResult(false, "message is required.");
        if (files.Count == 0)
            return new McpToolResult(false, "At least one file is required.");

        return await EnqueueAsync(
            target.WorktreePath!,
            new EnqueueRequest(slice, message, files, reviewIssueId),
            cancellationToken);
    }

    public async Task<McpToolResult> GetCommitsStatus(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);

        try
        {
            var status = await commits.GetStatusAsync(target.WorktreePath!, cancellationToken);
            var message = status.Entries.Count == 0
                ? "No queued commit entries."
                : $"{status.Entries.Count} queued entr{(status.Entries.Count == 1 ? "y" : "ies")}.";
            return new McpToolResult(true, message, status);
        }
        catch (InvalidOperationException)
        {
            return new McpToolResult(false, "Commit queue status is unavailable.");
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Commit queue status is unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Commit queue status is unavailable.");
        }
    }

    public async Task<McpToolResult> GuardCommits(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);

        try
        {
            var result = await commits.GuardAsync(target.WorktreePath!, cancellationToken);
            var message = result.Success && result.ContinueWorktreePath is not null
                ? $"Commit queue guard passed. Continue dependent work in {result.ContinueWorktreePath}."
                : result.Success
                    ? "Commit queue guard passed. It is safe to start a new task."
                : "Commit queue guard blocked starting new work. Stop unless the user asked to inspect, debug, or continue the existing queued or working-tree changes.";
            return new McpToolResult(result.Success, message, result);
        }
        catch (InvalidOperationException)
        {
            return new McpToolResult(false, "Commit queue guard is unavailable.");
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Commit queue guard is unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Commit queue guard is unavailable.");
        }
    }

    public async Task<McpToolResult> GetCommitChanges(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        var target = workspaces.Resolve(workspaceId, worktreePath);
        if (!target.Resolved)
            return TargetFailure(target);

        try
        {
            var changes = await commits.GetChangesAsync(target.WorktreePath!, cancellationToken);
            var message = changes.UnassignedFiles.Count == 0
                ? "No unassigned modified files."
                : $"{changes.UnassignedFiles.Count} unassigned modified file(s).";
            return new McpToolResult(true, message, changes);
        }
        catch (InvalidOperationException)
        {
            return new McpToolResult(false, "Commit queue changes are unavailable.");
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Commit queue changes are unavailable.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Commit queue changes are unavailable.");
        }
    }

    public Task<McpToolResult> InspectCommit(
        string entryRef,
        bool includePatch,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
        => EntryResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            entryRef,
            path => commits.InspectAsync(path, entryRef, includePatch, cancellationToken),
            result => new McpToolResult(true, $"Commit entry '{result.Entry.Slice}'.", result));

    public Task<McpToolResult> UpdateCommitMessage(
        string entryRef,
        string message,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Task.FromResult(new McpToolResult(false, "message is required."));

        return EntryResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            entryRef,
            path => commits.UpdateMessageAsync(path, entryRef, message, cancellationToken));
    }

    public Task<McpToolResult> AddCommitFiles(
        string entryRef,
        IReadOnlyList<string> files,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return Task.FromResult(new McpToolResult(false, "At least one file is required."));

        return EntryResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            entryRef,
            path => commits.AddFilesAsync(path, entryRef, files, cancellationToken));
    }

    public Task<McpToolResult> RemoveCommitFiles(
        string entryRef,
        IReadOnlyList<string> files,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return Task.FromResult(new McpToolResult(false, "At least one file is required."));

        return EntryResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            entryRef,
            path => commits.RemoveFilesAsync(path, entryRef, files, cancellationToken));
    }

    public Task<McpToolResult> RemoveCommit(
        string entryRef,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
        => EntryResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            entryRef,
            path => commits.RemoveAsync(path, entryRef, cancellationToken));

    public Task<McpToolResult> RestoreCommit(
        string entryId,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
        => EntryResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            entryId,
            path => commits.RestoreArchivedAsync(path, entryId, cancellationToken));

    public Task<McpToolResult> ClearCommits(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
        => WorktreeResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            path => commits.ClearAsync(path, cancellationToken));

    public Task<McpToolResult> BeginCommitEdit(
        string entryRef,
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
        => EntryResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            entryRef,
            path => commits.BeginEditAsync(path, entryRef, cancellationToken));

    public Task<McpToolResult> SaveCommitEdit(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
        => WorktreeResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            path => commits.SaveEditAsync(path, cancellationToken));

    public Task<McpToolResult> AbortCommitEdit(
        string? workspaceId,
        string? worktreePath,
        CancellationToken cancellationToken)
        => WorktreeResultAsync(
            workspaces.Resolve(workspaceId, worktreePath),
            path => commits.AbortEditAsync(path, cancellationToken));

    private async Task<McpToolResult> EnqueueAsync(
        string worktreePath,
        EnqueueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await commits.EnqueueAsync(worktreePath, request, cancellationToken);
            if (!result.Succeeded && result.Message.StartsWith("Queue operation failed:", StringComparison.Ordinal))
                return new McpToolResult(false, "Commit queue operation failed.");

            return new McpToolResult(result.Succeeded, result.Message, result);
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Commit queue operation failed.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Commit queue operation failed.");
        }
    }

    private static async Task<McpToolResult> WorktreeResultAsync(
        WorkspaceTargetResolution target,
        Func<string, Task<CommitEditResult>> operation)
    {
        if (!target.Resolved)
            return TargetFailure(target);

        try
        {
            return ToMcpResult(await operation(target.WorktreePath!));
        }
        catch (InvalidOperationException ex)
        {
            return new McpToolResult(false, ex.Message);
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Commit queue operation failed.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Commit queue operation failed.");
        }
    }

    private static Task<McpToolResult> EntryResultAsync(
        WorkspaceTargetResolution target,
        string entryRef,
        Func<string, Task<CommitEditResult>> operation)
        => EntryResultAsync(target, entryRef, operation, ToMcpResult);

    private static async Task<McpToolResult> EntryResultAsync<T>(
        WorkspaceTargetResolution target,
        string entryRef,
        Func<string, Task<T>> operation,
        Func<T, McpToolResult> map)
    {
        if (!target.Resolved)
            return TargetFailure(target);
        if (string.IsNullOrWhiteSpace(entryRef))
            return new McpToolResult(false, "entryRef is required.");

        try
        {
            return map(await operation(target.WorktreePath!));
        }
        catch (InvalidOperationException ex)
        {
            return new McpToolResult(false, ex.Message);
        }
        catch (IOException)
        {
            return new McpToolResult(false, "Commit queue operation failed.");
        }
        catch (UnauthorizedAccessException)
        {
            return new McpToolResult(false, "Commit queue operation failed.");
        }
    }

    private static McpToolResult TargetFailure(WorkspaceTargetResolution target)
        => new(false, target.Error!);

    private static McpToolResult ToMcpResult(CommitEditResult result)
        => new(result.Success, result.Message, result);
}
