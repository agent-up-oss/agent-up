using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Features.Commits.Services;

public sealed class CommitsService(ICommitsQueueProvider queue, ICommitsGitProvider git)
{
    public async Task<CommitsEnqueueResult> EnqueueAsync(string worktreePath, EnqueueRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var queueSize = 0;
            await queue.WithLockAsync(worktreePath, async ct =>
            {
                var current = await queue.ReadAsync(worktreePath, ct);
                if (current.ActiveSession is not null)
                    throw new InvalidOperationException("A commit queue edit session is active. Save or abort it first.");

                var owners = current.Commits
                    .SelectMany(e => e.Files.Select(f => (File: f, Entry: e)))
                    .ToList();
                var duplicate = request.Files.FirstOrDefault(file =>
                    owners.Any(o => string.Equals(o.File, file, StringComparison.OrdinalIgnoreCase)));
                if (duplicate is not null)
                    throw new InvalidOperationException($"File '{duplicate}' is already assigned to another queued entry.");

                var id = Guid.NewGuid().ToString("N");
                var entry = new CommitEntry(request.Slice, request.Message, request.Files, request.Tests, id, id);
                var patch = await git.GetDiffAsync(worktreePath, request.Files, ct);
                await queue.SavePatchAsync(worktreePath, entry.PatchKey, patch, ct);
                var updated = current with { Commits = [.. current.Commits, entry] };
                await queue.WriteAsync(worktreePath, updated, ct);
                try
                {
                    await git.RestoreFilesAsync(worktreePath, request.Files, ct);
                }
                catch (InvalidOperationException)
                {
                    await queue.WriteAsync(worktreePath, current, ct);
                    await queue.DeletePatchAsync(worktreePath, entry.PatchKey, ct);
                    throw;
                }
                catch (IOException)
                {
                    await queue.WriteAsync(worktreePath, current, ct);
                    await queue.DeletePatchAsync(worktreePath, entry.PatchKey, ct);
                    throw;
                }

                queueSize = updated.Commits.Count;
                return true;
            }, cancellationToken);

            return new CommitsEnqueueResult(true, EnqueuedMessage(request.Slice, queueSize), queueSize);
        }
        catch (InvalidOperationException ex)
        {
            return new CommitsEnqueueResult(false, ex.Message);
        }
        catch (IOException ex)
        {
            return new CommitsEnqueueResult(false, $"Queue operation failed: {ex.Message}");
        }
    }

    private static string EnqueuedMessage(string slice, int queueSize) =>
        $"""
        Enqueued '{slice}'. Queue size: {queueSize}.
        The tracked files have been restored to their pre-change state so the patch can be applied cleanly by 'agentup commits next'. Do NOT re-apply or modify those files - the queue owns them now.
        """;

    public async Task<CommitsStatusResult> GetStatusAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        var current = await queue.ReadAsync(worktreePath, cancellationToken);
        var assignedFiles = current.Commits.SelectMany(e => e.Files).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modified = await git.GetModifiedFilesAsync(worktreePath, cancellationToken);
        var unassigned = modified.Where(f => !assignedFiles.Contains(f)).ToList();
        var session = current.ActiveSession is null
            ? null
            : new CommitsStatusSession(current.ActiveSession.EntryId, current.ActiveSession.Files);
        var entries = current.Commits
            .Select(e => new CommitEntryDto(e.Slice, e.Message, e.Files, e.Tests, e.Id, e.PatchId))
            .ToList();
        return new CommitsStatusResult(entries, unassigned, session);
    }
}
