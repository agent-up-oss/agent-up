using AgentUp.CLI.Features.Commits.DTOs;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.Services;

public sealed class CommitsService(ICommitsQueueProvider queue, ICommitsGitProvider git)
{
    public async Task EnqueueAsync(EnqueueRequest request, CancellationToken cancellationToken = default)
    {
        var current = await queue.ReadAsync(cancellationToken);
        var entry = new CommitEntry(request.Slice, request.Message, request.Files, request.Tests);
        var updated = current with { Commits = [.. current.Commits, entry] };
        await queue.WriteAsync(updated, cancellationToken);

        var patch = await git.GetDiffAsync(request.Files, cancellationToken);
        await queue.SavePatchAsync(request.Slice, patch, cancellationToken);
        await git.RestoreFilesAsync(request.Files, cancellationToken);
    }

    public async Task<CommitsStatusResult> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var current = await queue.ReadAsync(cancellationToken);
        var assignedFiles = current.Commits.SelectMany(e => e.Files).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var modified = await git.GetModifiedFilesAsync(cancellationToken);
        var unassigned = modified.Where(f => !assignedFiles.Contains(f)).ToList();

        return new CommitsStatusResult(current.Commits, unassigned);
    }

    public async Task<CommitsStagingResult?> StageNextAsync(CancellationToken cancellationToken = default)
    {
        var current = await queue.ReadAsync(cancellationToken);
        if (current.Commits.Count == 0)
            return null;

        if (await git.HasStagedChangesAsync(cancellationToken))
            return CommitsStagingResult.Blocked("Staged changes are not yet committed. Commit them first, then run 'agentup commits next'.");

        var head = current.Commits[0];
        await git.ResetStagingAsync(cancellationToken);

        var patch = await queue.ReadPatchAsync(head.Slice, cancellationToken);
        if (patch is not null)
            await git.ApplyPatchAsync(patch, cancellationToken);

        await git.StageFilesAsync(head.Files, cancellationToken);

        var remaining = current.Commits.Skip(1).ToList();
        if (remaining.Count == 0)
            await queue.DeleteAsync(cancellationToken);
        else
            await queue.WriteAsync(current with { Commits = remaining }, cancellationToken);

        return new CommitsStagingResult(head.Slice, head.Message, head.Files, remaining.Count);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
        => await queue.DeleteAsync(cancellationToken);
}
