using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Shared.Interfaces;

namespace AgentUp.Server.Features.Commits.Services;

public sealed class CommitQueueMcpService(CommitsController commits)
{
    public async Task<McpToolResult> EnqueueCommit(
        string worktreePath,
        string slice,
        string message,
        IReadOnlyList<string> files,
        IReadOnlyList<string>? tests,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");
        if (string.IsNullOrWhiteSpace(slice))
            return new McpToolResult(false, "slice is required.");
        if (string.IsNullOrWhiteSpace(message))
            return new McpToolResult(false, "message is required.");
        if (files.Count == 0)
            return new McpToolResult(false, "At least one file is required.");

        try
        {
            var result = await commits.EnqueueAsync(worktreePath, new EnqueueRequest(slice, message, files, tests ?? []), cancellationToken);
            if (!result.Succeeded && result.Message.StartsWith("Queue operation failed:", StringComparison.Ordinal))
                return new McpToolResult(false, "Commit queue operation failed.");

            return new McpToolResult(result.Succeeded, result.Message);
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

    public async Task<McpToolResult> GetCommitsStatus(
        string worktreePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");

        try
        {
            var status = await commits.GetStatusAsync(worktreePath, cancellationToken);
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
        string worktreePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");

        try
        {
            var result = await commits.GuardAsync(worktreePath, cancellationToken);
            var message = result.Success
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
        string worktreePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");

        try
        {
            var changes = await commits.GetChangesAsync(worktreePath, cancellationToken);
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
        string worktreePath,
        string entryRef,
        bool includePatch,
        CancellationToken cancellationToken)
        => EntryResultAsync(
            worktreePath,
            entryRef,
            () => commits.InspectAsync(worktreePath, entryRef, includePatch, cancellationToken),
            result => new McpToolResult(true, $"Commit entry '{result.Entry.Slice}'.", result));

    public Task<McpToolResult> UpdateCommitMessage(
        string worktreePath,
        string entryRef,
        string message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Task.FromResult(new McpToolResult(false, "message is required."));

        return EntryResultAsync(worktreePath, entryRef, () => commits.UpdateMessageAsync(worktreePath, entryRef, message, cancellationToken));
    }

    public Task<McpToolResult> UpdateCommitTests(
        string worktreePath,
        string entryRef,
        IReadOnlyList<string> tests,
        CancellationToken cancellationToken)
        => EntryResultAsync(worktreePath, entryRef, () => commits.SetTestsAsync(worktreePath, entryRef, tests, cancellationToken));

    public Task<McpToolResult> AddCommitFiles(
        string worktreePath,
        string entryRef,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return Task.FromResult(new McpToolResult(false, "At least one file is required."));

        return EntryResultAsync(worktreePath, entryRef, () => commits.AddFilesAsync(worktreePath, entryRef, files, cancellationToken));
    }

    public Task<McpToolResult> RemoveCommitFiles(
        string worktreePath,
        string entryRef,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
            return Task.FromResult(new McpToolResult(false, "At least one file is required."));

        return EntryResultAsync(worktreePath, entryRef, () => commits.RemoveFilesAsync(worktreePath, entryRef, files, cancellationToken));
    }

    public Task<McpToolResult> RemoveCommit(
        string worktreePath,
        string entryRef,
        CancellationToken cancellationToken)
        => EntryResultAsync(worktreePath, entryRef, () => commits.RemoveAsync(worktreePath, entryRef, cancellationToken));

    public Task<McpToolResult> RestoreCommit(
        string worktreePath,
        string entryId,
        CancellationToken cancellationToken)
        => EntryResultAsync(worktreePath, entryId, () => commits.RestoreArchivedAsync(worktreePath, entryId, cancellationToken));

    public Task<McpToolResult> ClearCommits(
        string worktreePath,
        CancellationToken cancellationToken)
        => WorktreeResultAsync(worktreePath, () => commits.ClearAsync(worktreePath, cancellationToken));

    public Task<McpToolResult> BeginCommitEdit(
        string worktreePath,
        string entryRef,
        CancellationToken cancellationToken)
        => EntryResultAsync(worktreePath, entryRef, () => commits.BeginEditAsync(worktreePath, entryRef, cancellationToken));

    public Task<McpToolResult> SaveCommitEdit(
        string worktreePath,
        CancellationToken cancellationToken)
        => WorktreeResultAsync(worktreePath, () => commits.SaveEditAsync(worktreePath, cancellationToken));

    public Task<McpToolResult> AbortCommitEdit(
        string worktreePath,
        CancellationToken cancellationToken)
        => WorktreeResultAsync(worktreePath, () => commits.AbortEditAsync(worktreePath, cancellationToken));

    private async Task<McpToolResult> WorktreeResultAsync(string worktreePath, Func<Task<CommitEditResult>> operation)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");

        try
        {
            return ToMcpResult(await operation());
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

    private Task<McpToolResult> EntryResultAsync(
        string worktreePath,
        string entryRef,
        Func<Task<CommitEditResult>> operation)
        => EntryResultAsync(worktreePath, entryRef, operation, ToMcpResult);

    private async Task<McpToolResult> EntryResultAsync<T>(
        string worktreePath,
        string entryRef,
        Func<Task<T>> operation,
        Func<T, McpToolResult> map)
    {
        if (string.IsNullOrWhiteSpace(worktreePath))
            return new McpToolResult(false, "worktreePath is required.");
        if (string.IsNullOrWhiteSpace(entryRef))
            return new McpToolResult(false, "entryRef is required.");

        try
        {
            return map(await operation());
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

    private static McpToolResult ToMcpResult(CommitEditResult result)
        => new(result.Success, result.Message, result);
}
