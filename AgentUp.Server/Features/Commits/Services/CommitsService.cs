using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Verification.Controllers;

namespace AgentUp.Server.Features.Commits.Services;

public sealed class CommitsService
{
    private readonly ICommitsQueueProvider queue;
    private readonly ICommitsGitProvider git;
    private readonly CommitPolicyProvider commitPolicy;
    private readonly IProposalStackGitProvider? proposals;
    private readonly ICommitQueueConfigurationProvider? configuration;
    private readonly VerificationController? verification;

    public CommitsService(ICommitsQueueProvider queue, ICommitsGitProvider git, CommitPolicyProvider commitPolicy)
        : this(queue, git, commitPolicy, null, null, null)
    {
    }

    public CommitsService(
        ICommitsQueueProvider queue,
        ICommitsGitProvider git,
        CommitPolicyProvider commitPolicy,
        IProposalStackGitProvider? proposals,
        ICommitQueueConfigurationProvider? configuration,
        VerificationController? verification)
    {
        this.queue = queue;
        this.git = git;
        this.commitPolicy = commitPolicy;
        this.proposals = proposals;
        this.configuration = configuration;
        this.verification = verification;
    }

    public async Task<CommitsEnqueueResult> EnqueueAsync(string worktreePath, EnqueueRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var queueSize = 0;
            await queue.WithLockAsync(worktreePath, async ct =>
            {
                var current = await queue.ReadAsync(worktreePath, ct);
                await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
                if (current.ActiveSession is not null)
                    throw new InvalidOperationException("A commit queue edit session is active. Save or abort it first.");

                commitPolicy.Validate(request.Slice, request.Message, request.Files);
                EnsureReviewIssueIsUnassigned(current, request.ReviewIssueId);
                var useProposalStack = proposals is not null
                    && (configuration?.IsGitQueueEnabled(worktreePath) is true || current.QueueId is not null);
                if (useProposalStack)
                {
                    var gate = verification is null
                        ? null
                        : await verification.RunAndGuardAsync(worktreePath, ct);
                    if (gate is not null && !gate.Succeeded)
                        throw new InvalidOperationException(gate.Message);

                    var queueId = current.QueueId ?? Guid.NewGuid().ToString("N");
                    var proposal = await proposals!.EnqueueAsync(worktreePath, current, queueId, request.Message, request.Files, ct);
                    var proposalEntryId = Guid.NewGuid().ToString("N");
                    var proposalEntry = new CommitEntry(
                        request.Slice,
                        request.Message,
                        request.Files,
                        proposalEntryId,
                        proposalEntryId,
                        NormalizeOptional(request.ReviewIssueId),
                        proposal.ParentCommit,
                        proposal.Commit,
                        gate is null ? "unverified" : "ready");
                    await queue.SavePatchAsync(worktreePath, proposalEntry.PatchKey, proposal.Patch, ct);
                    var proposalQueue = current with
                    {
                        Version = 3,
                        Commits = [.. current.Commits, proposalEntry],
                        QueueId = queueId,
                        BaseCommit = proposal.BaseCommit,
                        TipCommit = proposal.Commit,
                        QueueWorktreePath = proposal.QueueWorktreePath,
                        Generation = current.Generation + 1
                    };
                    await queue.WriteAsync(worktreePath, proposalQueue, ct);
                    queueSize = proposalQueue.Commits.Count;
                    return true;
                }

                var owners = current.Commits
                    .SelectMany(e => e.Files.Select(f => (File: f, Entry: e)))
                    .ToList();
                var duplicate = request.Files.FirstOrDefault(file =>
                    owners.Any(o => string.Equals(o.File, file, StringComparison.OrdinalIgnoreCase)));
                if (duplicate is not null)
                    throw new InvalidOperationException($"File '{duplicate}' is already assigned to another queued entry.");

                var id = Guid.NewGuid().ToString("N");
                var entry = new CommitEntry(request.Slice, request.Message, request.Files, id, id, NormalizeOptional(request.ReviewIssueId));
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

            var stored = await queue.ReadAsync(worktreePath, cancellationToken);
            var proposalStackEnabled = stored.QueueWorktreePath is not null;
            var message = !proposalStackEnabled
                ? EnqueuedMessage(request.Slice, queueSize)
                : stored.Commits.Last().State == "ready"
                    ? $"Enqueued and verified '{request.Slice}' as proposal {stored.TipCommit}. Continue dependent work in {stored.QueueWorktreePath}."
                    : $"Enqueued '{request.Slice}' as unverified proposal {stored.TipCommit}. Continue dependent work in {stored.QueueWorktreePath}.";
            return new CommitsEnqueueResult(true, message, queueSize, stored.QueueWorktreePath, stored.Generation);
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
        The tracked files have been restored to their pre-change state so the patch can be applied cleanly by 'agent-up commits next'. Do NOT re-apply or modify those files - the queue owns them now.
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
        var operation = await git.GetOperationStateAsync(worktreePath, cancellationToken);
        var entries = current.Commits
            .Select(e => new CommitEntryDto(e.Slice, e.Message, e.Files, e.Id, e.PatchId, e.ReviewIssueId, e.ParentCommit, e.ProposalCommit, e.State))
            .ToList();
        return new CommitsStatusResult(entries, unassigned, session, operation, current.QueueWorktreePath, current.BaseCommit, current.TipCommit, current.Generation);
    }

    public async Task<CommitChangesResult> GetChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        var current = await queue.ReadAsync(worktreePath, cancellationToken);
        var assignedFiles = current.Commits.SelectMany(e => e.Files).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var modified = await git.GetModifiedFilesAsync(worktreePath, cancellationToken);
        var staged = await git.GetStagedFilesAsync(worktreePath, cancellationToken);
        var untracked = await git.GetUntrackedFilesAsync(worktreePath, cancellationToken);
        var unassigned = modified.Where(f => !assignedFiles.Contains(f)).ToList();
        return new CommitChangesResult(modified, staged, untracked, assignedFiles.Order(StringComparer.OrdinalIgnoreCase).ToList(), unassigned);
    }

    public async Task<CommitInspectResult> InspectAsync(string worktreePath, string entryRef, bool includePatch, CancellationToken cancellationToken = default)
    {
        var current = await queue.ReadAsync(worktreePath, cancellationToken);
        var entry = ResolveEntry(current, entryRef);
        var patch = includePatch ? await queue.ReadPatchAsync(worktreePath, entry.PatchKey, cancellationToken) : null;
        return new CommitInspectResult(entry, patch);
    }

    public Task<CommitEditResult> UpdateMessageAsync(string worktreePath, string entryRef, string message, CancellationToken cancellationToken = default)
        => UpdateEntryAsync(worktreePath, entryRef, entry => entry with { Message = message }, cancellationToken);

    public async Task<CommitEditResult> AddFilesAsync(string worktreePath, string entryRef, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        => await queue.WithLockAsync(worktreePath, async ct =>
        {
            var current = await queue.ReadAsync(worktreePath, ct);
            await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
            var entry = ResolveEntry(current, entryRef);
            EnsureNotEditingEntry(current, entry);
            EnsureFilesAreUnassigned(current, files, entry.Id);
            var updatedFiles = entry.Files.Concat(files).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var updatedEntry = entry with { Files = updatedFiles };
            commitPolicy.Validate(updatedEntry.Slice, updatedEntry.Message, updatedEntry.Files);
            await queue.WriteAsync(worktreePath, ReplaceEntry(current, updatedEntry), ct);
            return CommitEditResult.Completed("Files added.", updatedEntry, current.ActiveSession);
        }, cancellationToken);

    public Task<CommitEditResult> RemoveFilesAsync(string worktreePath, string entryRef, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        => UpdateEntryAsync(worktreePath, entryRef, entry => entry with
        {
            Files = entry.Files.Where(f => !files.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList()
        }, cancellationToken);

    public async Task<CommitEditResult> RemoveAsync(string worktreePath, string entryRef, CancellationToken cancellationToken = default)
        => await queue.WithLockAsync(worktreePath, async ct =>
        {
            var current = await queue.ReadAsync(worktreePath, ct);
            await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
            EnsureNoActiveSession(current);
            var entry = ResolveEntry(current, entryRef);
            var remaining = current.Commits.Where(e => e.Id != entry.Id).ToList();
            await queue.WriteAsync(worktreePath, current with { Commits = remaining, Archive = Archive(current, [entry]) }, ct);
            return CommitEditResult.Completed("Entry archived.", entry);
        }, cancellationToken);

    public async Task<CommitEditResult> RestoreArchivedAsync(string worktreePath, string entryId, CancellationToken cancellationToken = default)
        => await queue.WithLockAsync(worktreePath, async ct =>
        {
            var current = await queue.ReadAsync(worktreePath, ct);
            await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
            EnsureNoActiveSession(current);
            var archived = current.Archived.FirstOrDefault(a => a.Entry.Id == entryId)
                ?? throw new InvalidOperationException($"No archived commit entry matches '{entryId}'.");
            EnsureFilesAreUnassigned(current, archived.Entry.Files);

            var archive = current.Archived.Where(a => a.Entry.Id != entryId).ToList();
            await queue.WriteAsync(worktreePath, current with { Commits = [.. current.Commits, archived.Entry], Archive = archive }, ct);
            return CommitEditResult.Completed("Entry restored.", archived.Entry);
        }, cancellationToken);

    public async Task<CommitEditResult> ClearAsync(string worktreePath, CancellationToken cancellationToken = default)
        => await queue.WithLockAsync(worktreePath, async ct =>
        {
            var current = await queue.ReadAsync(worktreePath, ct);
            await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
            EnsureNoActiveSession(current);
            var archived = Archive(current, current.Commits);
            await queue.WriteAsync(worktreePath, current with { Commits = [], Archive = archived }, ct);
            return CommitEditResult.Completed("Queue cleared.");
        }, cancellationToken);

    public async Task<CommitEditResult> BeginEditAsync(string worktreePath, string entryRef, CancellationToken cancellationToken = default)
        => await queue.WithLockAsync(worktreePath, async ct =>
        {
            var current = await queue.ReadAsync(worktreePath, ct);
            await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
            EnsureNoActiveSession(current);
            if ((await git.GetModifiedFilesAsync(worktreePath, ct)).Count > 0 || await git.HasStagedChangesAsync(worktreePath, ct))
                return CommitEditResult.Blocked("Working tree must be clean before starting a commit queue edit session.");

            var entry = ResolveEntry(current, entryRef);
            var patch = await queue.ReadPatchAsync(worktreePath, entry.PatchKey, ct);
            var session = new CommitEditSession(entry.Id, entry.PatchKey, entry.Files);
            if (patch is not null)
                await git.ApplyPatchAsync(worktreePath, patch, ct);
            await queue.WriteAsync(worktreePath, current with { ActiveSession = session }, ct);

            return CommitEditResult.Completed("Edit session started.", entry, session);
        }, cancellationToken);

    public async Task<CommitEditResult> SaveEditAsync(string worktreePath, CancellationToken cancellationToken = default)
        => await queue.WithLockAsync(worktreePath, async ct =>
        {
            var current = await queue.ReadAsync(worktreePath, ct);
            await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
            var session = current.ActiveSession ?? throw new InvalidOperationException("No commit queue edit session is active.");
            var entry = ResolveEntry(current, session.EntryId);
            var modified = await git.GetModifiedFilesAsync(worktreePath, ct);
            var outside = modified.Where(f => !entry.Files.Contains(f, StringComparer.OrdinalIgnoreCase)).ToList();
            if (outside.Count > 0)
                return CommitEditResult.Blocked($"Edit session has changes outside queued files: {string.Join(", ", outside)}");
            if (await git.HasStagedChangesAsync(worktreePath, ct))
                return CommitEditResult.Blocked("Edit session cannot be saved while files are staged.");

            var patchId = Guid.NewGuid().ToString("N");
            var patch = await git.GetDiffAsync(worktreePath, entry.Files, ct);
            await queue.SavePatchAsync(worktreePath, patchId, patch, ct);
            var updatedEntry = entry with { PatchId = patchId };
            await git.RestoreFilesAsync(worktreePath, entry.Files, ct);
            await queue.WriteAsync(worktreePath, ReplaceEntry(current with { ActiveSession = null }, updatedEntry), ct);
            return CommitEditResult.Completed("Edit session saved.", updatedEntry);
        }, cancellationToken);

    public async Task<CommitEditResult> AbortEditAsync(string worktreePath, CancellationToken cancellationToken = default)
        => await queue.WithLockAsync(worktreePath, async ct =>
        {
            var current = await queue.ReadAsync(worktreePath, ct);
            await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
            var session = current.ActiveSession ?? throw new InvalidOperationException("No commit queue edit session is active.");
            await git.RestoreFilesAsync(worktreePath, session.Files, ct);
            await queue.WriteAsync(worktreePath, current with { ActiveSession = null }, ct);
            return CommitEditResult.Completed("Edit session aborted.", session: session);
        }, cancellationToken);

    public async Task<CommitGuardResult> GuardAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        var current = await queue.ReadAsync(worktreePath, cancellationToken);
        var blockers = new List<string>();
        var proposalQueue = current.QueueWorktreePath is not null;
        if (current.Commits.Count > 0 && !proposalQueue)
            blockers.Add($"{current.Commits.Count} commit queue entr{(current.Commits.Count == 1 ? "y is" : "ies are")} still queued.");
        if (current.ActiveSession is not null)
            blockers.Add("A commit queue edit session is active.");
        if (await git.HasStagedChangesAsync(worktreePath, cancellationToken))
            blockers.Add("Staged changes are present.");
        var operation = await git.GetOperationStateAsync(worktreePath, cancellationToken);
        if (operation.Blocking)
            blockers.Add($"A Git {operation.Kind} is in progress. Finish or abort it before using the commit queue.");
        var unassigned = (await GetStatusAsync(worktreePath, cancellationToken)).UnassignedFiles;
        if (unassigned.Count > 0)
            blockers.Add($"{unassigned.Count} modified file(s) are not assigned to a queue entry.");

        return blockers.Count == 0
            ? CommitGuardResult.Passed(current.QueueWorktreePath)
            : CommitGuardResult.Failed(blockers);
    }

    private async Task<CommitEditResult> UpdateEntryAsync(string worktreePath, string entryRef, Func<CommitEntry, CommitEntry> update, CancellationToken cancellationToken)
    {
        try
        {
            return await queue.WithLockAsync(worktreePath, async ct =>
            {
                var current = await queue.ReadAsync(worktreePath, ct);
                await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
                var entry = ResolveEntry(current, entryRef);
                EnsureNotEditingEntry(current, entry);
                var updatedEntry = update(entry);
                commitPolicy.Validate(updatedEntry.Slice, updatedEntry.Message, updatedEntry.Files);
                await queue.WriteAsync(worktreePath, ReplaceEntry(current, updatedEntry), ct);
                return CommitEditResult.Completed("Entry updated.", updatedEntry, current.ActiveSession);
            }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return CommitEditResult.Blocked(ex.Message);
        }
        catch (IOException ex)
        {
            return CommitEditResult.Blocked($"Queue operation failed: {ex.Message}");
        }
    }

    private static CommitsQueue ReplaceEntry(CommitsQueue queueState, CommitEntry updatedEntry)
        => queueState with
        {
            Commits = queueState.Commits.Select(e => e.Id == updatedEntry.Id ? updatedEntry : e).ToList()
        };

    private static CommitEntry ResolveEntry(CommitsQueue current, string entryRef)
    {
        if (int.TryParse(entryRef, out var index) && index >= 1 && index <= current.Commits.Count)
            return current.Commits[index - 1];

        return current.Commits.FirstOrDefault(e => e.Id == entryRef)
            ?? throw new InvalidOperationException($"No queued commit entry matches '{entryRef}'.");
    }

    private static void EnsureNoActiveSession(CommitsQueue current)
    {
        if (current.ActiveSession is not null)
            throw new InvalidOperationException("A commit queue edit session is active. Save or abort it first.");
    }

    private static void EnsureNotEditingEntry(CommitsQueue current, CommitEntry entry)
    {
        if (current.ActiveSession?.EntryId == entry.Id)
            throw new InvalidOperationException("Cannot mutate files or metadata for the entry currently under edit. Save or abort the edit session first.");
    }

    private static void EnsureFilesAreUnassigned(CommitsQueue current, IReadOnlyList<string> files, string? allowedEntryId = null)
    {
        var owners = current.Commits
            .Where(e => allowedEntryId is null || e.Id != allowedEntryId)
            .SelectMany(e => e.Files.Select(f => (File: f, Entry: e)))
            .ToList();
        var duplicate = files.FirstOrDefault(file => owners.Any(o => string.Equals(o.File, file, StringComparison.OrdinalIgnoreCase)));
        if (duplicate is not null)
            throw new InvalidOperationException($"File '{duplicate}' is already assigned to another queued entry.");
    }

    private async Task EnsureNoBlockingGitOperationAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var operation = await git.GetOperationStateAsync(worktreePath, cancellationToken);
        if (operation.Blocking)
            throw new InvalidOperationException($"A Git {operation.Kind} is in progress. Finish or abort it before using the commit queue.");
    }
    private static void EnsureReviewIssueIsUnassigned(CommitsQueue current, string? reviewIssueId)
    {
        var normalized = NormalizeOptional(reviewIssueId);
        if (normalized is null)
            return;

        if (current.Commits.Any(e => string.Equals(NormalizeOptional(e.ReviewIssueId), normalized, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"Review issue '{normalized}' is already assigned to a queued commit.");
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<ArchivedCommitEntry> Archive(CommitsQueue current, IReadOnlyList<CommitEntry> entries)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return [.. current.Archived, .. entries.Select(entry => new ArchivedCommitEntry(entry, now))];
    }
}
