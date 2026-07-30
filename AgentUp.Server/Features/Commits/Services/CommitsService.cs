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
                await EnsureNoBlockingGitOperationAsync(worktreePath, ct);
                if (current.ActiveSession is not null)
                    throw new InvalidOperationException("A commit queue edit session is active. Save or abort it first.");

                ValidateConventionalCommit(request.Slice, request.Message, request.Files);
                ValidateSliceBoundary(request);
                EnsureReviewIssueIsUnassigned(current, request.ReviewIssueId);
                var owners = current.Commits
                    .SelectMany(e => e.Files.Select(f => (File: f, Entry: e)))
                    .ToList();
                var duplicate = request.Files.FirstOrDefault(file =>
                    owners.Any(o => string.Equals(o.File, file, StringComparison.OrdinalIgnoreCase)));
                if (duplicate is not null)
                    throw new InvalidOperationException($"File '{duplicate}' is already assigned to another queued entry.");

                var id = Guid.NewGuid().ToString("N");
                var entry = new CommitEntry(request.Slice, request.Message, request.Files, request.Tests, id, id, NormalizeOptional(request.ReviewIssueId));
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
        var operation = await git.GetOperationStateAsync(worktreePath, cancellationToken);
        var entries = current.Commits
            .Select(e => new CommitEntryDto(e.Slice, e.Message, e.Files, e.Tests, e.Id, e.PatchId, e.ReviewIssueId))
            .ToList();
        return new CommitsStatusResult(entries, unassigned, session, operation);
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

    public Task<CommitEditResult> SetTestsAsync(string worktreePath, string entryRef, IReadOnlyList<string> tests, CancellationToken cancellationToken = default)
        => UpdateEntryAsync(worktreePath, entryRef, entry => entry with { Tests = tests }, cancellationToken);

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
            ValidateConventionalCommit(updatedEntry.Slice, updatedEntry.Message, updatedEntry.Files);
            ValidateSliceBoundary(new EnqueueRequest(updatedEntry.Slice, updatedEntry.Message, updatedEntry.Files, updatedEntry.Tests, updatedEntry.ReviewIssueId));
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
        if (current.Commits.Count > 0)
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

        return blockers.Count == 0 ? CommitGuardResult.Passed() : CommitGuardResult.Failed(blockers);
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
                ValidateConventionalCommit(updatedEntry.Slice, updatedEntry.Message, updatedEntry.Files);
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

    private static void ValidateConventionalCommit(string slice, string message, IReadOnlyList<string> files)
    {
        var commit = ConventionalCommitParts(message);
        if (commit is null)
            throw new InvalidOperationException("Commit message must start with one of: feat, fix, test, chore, refactor, style, docs.");

        var prefix = commit.Value.Prefix;
        if (string.IsNullOrWhiteSpace(commit.Value.Scope))
            throw new InvalidOperationException("Commit message must include a scope matching the queued slice, such as fix(Commits): validate queue metadata.");

        if (NormalizeSliceName(commit.Value.Scope) != NormalizeSliceName(slice))
            throw new InvalidOperationException($"Commit message scope '{commit.Value.Scope}' does not match queued slice '{slice}'.");

        if (prefix == "docs" && files.Any(file => !IsDocumentationFile(file)))
            throw new InvalidOperationException("docs commits may only include documentation files such as docs/*, README*, AGENTS.md, CONTRIBUTING.md, SECURITY.md, CODE_OF_CONDUCT.md, or CHANGELOG.md.");

        if (prefix == "style" && files.Any(file => !IsStyleFile(file)))
            throw new InvalidOperationException("style commits may only include CSS or HTML files.");

        if (prefix == "test" && files.Any(file => !IsTestOrPackageSmokeFile(file)))
            throw new InvalidOperationException("test commits may only include test or smoke-validation files. Use fix or feat when production changes and same-slice tests are queued together.");

        if (prefix == "chore" && files.Any(file => !IsMaintenanceFile(file)))
            throw new InvalidOperationException("chore commits may only include maintenance, packaging, CI, or tool configuration files. Use fix, feat, or refactor for source/runtime behavior changes.");
    }

    private static (string Prefix, string? Scope)? ConventionalCommitParts(string message)
    {
        var separator = message.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0)
            return null;

        var type = message[..separator];
        var scopeStart = type.IndexOf('(', StringComparison.Ordinal);
        string? scope = null;
        if (scopeStart >= 0)
        {
            var scopeEnd = type.IndexOf(')', scopeStart + 1);
            if (scopeEnd <= scopeStart + 1)
                return null;
            scope = type[(scopeStart + 1)..scopeEnd];
            type = type[..scopeStart];
        }

        if (type.EndsWith('!'))
            type = type[..^1];

        return type is "feat" or "fix" or "test" or "chore" or "refactor" or "style" or "docs"
            ? (type, scope)
            : null;
    }

    private static bool IsDocumentationFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        var fileName = Path.GetFileName(normalized);
        return normalized.StartsWith("docs/", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("README", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "AGENTS.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "CONTRIBUTING.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "SECURITY.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "CODE_OF_CONDUCT.md", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "CHANGELOG.md", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStyleFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".css", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTestOrPackageSmokeFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var fileName = Path.GetFileName(normalized);
        return segments.Any(segment =>
                segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "tests", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "test", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "spec", StringComparison.OrdinalIgnoreCase)
                || string.Equals(segment, "specs", StringComparison.OrdinalIgnoreCase)
                || segment.Contains("PackageSmoke", StringComparison.OrdinalIgnoreCase))
            || fileName.Contains("Tests.", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("Test.", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".spec.", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".test.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMaintenanceFile(string path)
    {
        var normalized = path.Replace('\\', '/');
        var root = RootDirectory(normalized);
        var fileName = Path.GetFileName(normalized);
        var extension = Path.GetExtension(fileName);
        return normalized.StartsWith(".github/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".devcontainer/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(".vscode/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("packaging/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("scripts/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("tools/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(root, "packaging", StringComparison.OrdinalIgnoreCase)
            || string.Equals(root, "scripts", StringComparison.OrdinalIgnoreCase)
            || string.Equals(root, "tools", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "Dockerfile", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "docker-compose.yml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "docker-compose.yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "global.json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "nuget.config", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("Directory.Build.", StringComparison.OrdinalIgnoreCase)
            || fileName.StartsWith("Directory.Packages.", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".props", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".targets", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".editorconfig", StringComparison.OrdinalIgnoreCase);
    }

    private static string RootDirectory(string path)
        => path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    private static void ValidateSliceBoundary(EnqueueRequest request)
    {
        var featureSlices = request.Files
            .Select(FeatureSlice)
            .Where(slice => slice is not null)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToList();
        if (featureSlices.Count == 0)
            return;
        if (featureSlices.Count > 1)
            throw new InvalidOperationException($"Commit spans multiple feature slices: {string.Join(", ", featureSlices)}. Create one queued commit per slice.");

        var expected = NormalizeSliceName(featureSlices[0]);
        var actual = NormalizeSliceName(request.Slice);
        if (actual != expected)
            throw new InvalidOperationException($"Commit slice '{request.Slice}' does not match feature slice '{featureSlices[0]}'.");
    }

    private static string? FeatureSlice(string path)
    {
        var parts = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i + 1 < parts.Length; i++)
        {
            if (string.Equals(parts[i], "Features", StringComparison.OrdinalIgnoreCase))
                return parts[i + 1];
        }

        return null;
    }

    private static string NormalizeSliceName(string value)
    {
        var last = value.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
        return string.Concat(last.Where(char.IsLetterOrDigit)).ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<ArchivedCommitEntry> Archive(CommitsQueue current, IReadOnlyList<CommitEntry> entries)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        return [.. current.Archived, .. entries.Select(entry => new ArchivedCommitEntry(entry, now))];
    }
}
