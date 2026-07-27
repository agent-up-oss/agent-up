using AgentUp.CLI.Features.Commits.DTOs;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;
using AgentUp.CLI.Features.Commits.Services;

namespace AgentUp.CLI.Tests.Features.Commits.Unit;

[TestFixture]
public sealed class CommitsServiceTests
{
    [Test]
    public async Task EnqueueAsync_appendsEntryToEmptyQueue()
    {
        var queue = new FakeCommitsQueueProvider();
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        await service.EnqueueAsync(new EnqueueRequest("MySlice", "feat: add thing", ["a.cs"], []));

        Assert.That(queue.Stored!.Commits, Has.Count.EqualTo(1));
        Assert.That(queue.Stored.Commits[0].Slice, Is.EqualTo("MySlice"));
        Assert.That(queue.Stored.Commits[0].Message, Is.EqualTo("feat: add thing"));
        Assert.That(queue.Stored.Commits[0].Files, Is.EqualTo(new[] { "a.cs" }));
    }

    [Test]
    public async Task EnqueueAsync_appendsToExistingQueue()
    {
        var existing = new CommitsQueue(1, [new CommitEntry("First", "fix: first", ["x.cs"], [])]);
        var queue = new FakeCommitsQueueProvider(existing);
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        await service.EnqueueAsync(new EnqueueRequest("Second", "fix: second", ["y.cs"], []));

        Assert.That(queue.Stored!.Commits, Has.Count.EqualTo(2));
        Assert.That(queue.Stored.Commits[1].Slice, Is.EqualTo("Second"));
    }

    [Test]
    public async Task GetStatusAsync_returnsEmptyEntriesWhenQueueIsEmpty()
    {
        var service = new CommitsService(new FakeCommitsQueueProvider(), new FakeCommitsGitProvider());

        var result = await service.GetStatusAsync();

        Assert.That(result.Entries, Is.Empty);
        Assert.That(result.UnassignedFiles, Is.Empty);
    }

    [Test]
    public async Task GetStatusAsync_flagsModifiedFilesNotInAnyEntry()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("Slice", "msg", ["owned.cs"], [])
        ]));
        var git = new FakeCommitsGitProvider(modifiedFiles: ["owned.cs", "unassigned.cs"]);
        var service = new CommitsService(queue, git);

        var result = await service.GetStatusAsync();

        Assert.That(result.UnassignedFiles, Is.EqualTo(new[] { "unassigned.cs" }));
    }

    [Test]
    public async Task GetStatusAsync_doesNotFlagAssignedFiles()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("Slice", "msg", ["a.cs", "b.cs"], [])
        ]));
        var git = new FakeCommitsGitProvider(modifiedFiles: ["a.cs", "b.cs"]);
        var service = new CommitsService(queue, git);

        var result = await service.GetStatusAsync();

        Assert.That(result.UnassignedFiles, Is.Empty);
    }

    [Test]
    public async Task StageNextAsync_returnsNullWhenQueueIsEmpty()
    {
        var service = new CommitsService(new FakeCommitsQueueProvider(), new FakeCommitsGitProvider());

        var result = await service.StageNextAsync();

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task StageNextAsync_stagesHeadEntryAndStoresEmptyQueueWhenNowEmpty()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], []);
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [entry]));
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        var result = await service.StageNextAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Slice, Is.EqualTo("Slice"));
        Assert.That(result.StagedFiles, Is.EqualTo(new[] { "a.cs" }));
        Assert.That(result.RemainingCount, Is.EqualTo(0));
        Assert.That(git.StagedFiles, Is.EqualTo(new[] { "a.cs" }));
        Assert.That(queue.Stored!.Commits, Is.Empty);
    }

    [Test]
    public async Task StageNextAsync_popsHeadAndKeepsRemainingEntries()
    {
        var first = new CommitEntry("First", "fix: first", ["a.cs"], []);
        var second = new CommitEntry("Second", "fix: second", ["b.cs"], []);
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [first, second]));
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        var result = await service.StageNextAsync();

        Assert.That(result!.Slice, Is.EqualTo("First"));
        Assert.That(result.RemainingCount, Is.EqualTo(1));
        Assert.That(queue.Stored!.Commits, Has.Count.EqualTo(1));
        Assert.That(queue.Stored.Commits[0].Slice, Is.EqualTo("Second"));
        Assert.That(queue.Deleted, Is.False);
    }

    [Test]
    public async Task StageNextAsync_resetsStagingBeforeStaging()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [entry]));
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        await service.StageNextAsync();

        Assert.That(git.Invocations, Does.Contain("reset"));
        Assert.That(git.Invocations.IndexOf("reset"), Is.LessThan(git.Invocations.IndexOf("stage:a.cs")));
    }

    [Test]
    public async Task EnqueueAsync_savesPatchForEntry()
    {
        var queue = new FakeCommitsQueueProvider();
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        await service.EnqueueAsync(new EnqueueRequest("MySlice", "feat: add thing", ["a.cs"], []));

        Assert.That(git.DiffRequested, Is.True);
        var entry = queue.Stored!.Commits.Single();
        Assert.That(entry.Id, Is.Not.Empty);
        Assert.That(queue.Patches[entry.Id], Is.EqualTo("diff --git a/a.cs b/a.cs\n"));
    }

    [Test]
    public async Task EnqueueAsync_restoresWorkingTreeAfterSavingPatch()
    {
        var queue = new FakeCommitsQueueProvider();
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        await service.EnqueueAsync(new EnqueueRequest("MySlice", "feat: add thing", ["a.cs"], []));

        Assert.That(git.FilesRestored, Is.True);
        Assert.That(git.Invocations.IndexOf("diff:a.cs"), Is.LessThan(git.Invocations.IndexOf("restore:a.cs")));
    }

    [Test]
    public async Task StageNextAsync_appliesSavedPatchBeforeStaging()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [entry]));
        queue.Patches["entry-1"] = "diff --git a/a.cs b/a.cs\n";
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        await service.StageNextAsync();

        Assert.That(git.AppliedPatches, Is.EqualTo(new[] { "diff --git a/a.cs b/a.cs\n" }));
        Assert.That(git.Invocations.IndexOf("apply"), Is.LessThan(git.Invocations.IndexOf("stage:a.cs")));
    }

    [Test]
    public async Task StageNextAsync_whenStagedChangesExist_returnsBlockedResult()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], []);
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [entry]));
        var git = new FakeCommitsGitProvider(hasStagedChanges: true);
        var service = new CommitsService(queue, git);

        var result = await service.StageNextAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsBlocked, Is.True);
        Assert.That(result.BlockedReason, Does.Contain("not yet committed"));
    }

    [Test]
    public void EnqueueAsync_rejectsFilesAlreadyAssignedToAnotherEntry()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("First", "fix: first", ["a.cs"], [], "entry-1")
        ]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.EnqueueAsync(new EnqueueRequest("Second", "fix: second", ["a.cs"], [])));
    }

    [Test]
    public async Task StageNextAsync_whenEditSessionIsActive_returnsBlockedResult()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry], new CommitEditSession("entry-1", "entry-1", ["a.cs"])));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.StageNextAsync();

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsBlocked, Is.True);
        Assert.That(result.BlockedReason, Does.Contain("edit session"));
    }

    [Test]
    public async Task BeginEditAsync_appliesPatchAndStoresActiveSession()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry]));
        queue.Patches["entry-1"] = "diff --git a/a.cs b/a.cs\n";
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        var result = await service.BeginEditAsync("1");

        Assert.That(result.Success, Is.True);
        Assert.That(queue.Stored!.ActiveSession!.EntryId, Is.EqualTo("entry-1"));
        Assert.That(git.AppliedPatches, Is.EqualTo(new[] { "diff --git a/a.cs b/a.cs\n" }));
    }

    [Test]
    public async Task SaveEditAsync_rejectsChangesOutsideEntryFiles()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry], new CommitEditSession("entry-1", "entry-1", ["a.cs"])));
        var git = new FakeCommitsGitProvider(modifiedFiles: ["a.cs", "other.cs"]);
        var service = new CommitsService(queue, git);

        var result = await service.SaveEditAsync();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("outside queued files"));
    }

    [Test]
    public async Task SaveEditAsync_capturesNewPatchAndClearsSession()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], [], "entry-1", "patch-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry], new CommitEditSession("entry-1", "patch-1", ["a.cs"])));
        var git = new FakeCommitsGitProvider(modifiedFiles: ["a.cs"]);
        var service = new CommitsService(queue, git);

        var result = await service.SaveEditAsync();

        Assert.That(result.Success, Is.True);
        Assert.That(queue.Stored!.ActiveSession, Is.Null);
        Assert.That(queue.Stored.Commits[0].PatchKey, Is.Not.EqualTo("patch-1"));
        Assert.That(queue.Patches, Has.Count.EqualTo(1));
        Assert.That(git.FilesRestored, Is.True);
    }

    [Test]
    public async Task GuardAsync_failsWhenQueueHasEntries()
    {
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], [], "entry-1");
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [entry]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.GuardAsync();

        Assert.That(result.Success, Is.False);
        Assert.That(result.Blockers.Single(), Does.Contain("still queued"));
    }

    [Test]
    public async Task ClearAsync_archivesEntriesAndStoresEmptyQueue()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("S", "m", ["f.cs"], [])
        ]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        await service.ClearAsync();

        Assert.That(queue.Stored!.Commits, Is.Empty);
        Assert.That(queue.Stored.Archived, Has.Count.EqualTo(1));
    }

    // ── fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public CommitsQueue? Stored { get; private set; } = initial;
        public bool Deleted { get; private set; }
        public Dictionary<string, string> Patches { get; } = [];

        public Task<CommitsQueue> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Stored ?? CommitsQueue.Empty());

        public Task WriteAsync(CommitsQueue queue, CancellationToken cancellationToken = default)
        {
            Stored = queue;
            Deleted = false;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            Deleted = true;
            Stored = null;
            return Task.CompletedTask;
        }

        public Task SavePatchAsync(string patchKey, string patch, CancellationToken cancellationToken = default)
        {
            Patches[patchKey] = patch;
            return Task.CompletedTask;
        }

        public Task<string?> ReadPatchAsync(string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Patches.GetValueOrDefault(patchKey));

        public Task<T> WithLockAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeCommitsGitProvider(bool hasStagedChanges = false, string[]? modifiedFiles = null) : ICommitsGitProvider
    {
        public List<string> StagedFiles { get; } = [];
        public List<string> AppliedPatches { get; } = [];
        public List<string> Invocations { get; } = [];
        public bool StagingReset { get; private set; }
        public bool FilesRestored { get; private set; }

        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(modifiedFiles ?? []);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public bool DiffRequested { get; private set; }

        public Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            DiffRequested = true;
            Invocations.Add($"diff:{string.Join(",", files)}");
            return Task.FromResult("diff --git a/a.cs b/a.cs\n");
        }

        public Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(hasStagedChanges);

        public Task ApplyPatchAsync(string patch, CancellationToken cancellationToken = default)
        {
            AppliedPatches.Add(patch);
            Invocations.Add("apply");
            return Task.CompletedTask;
        }

        public Task RestoreFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            FilesRestored = true;
            Invocations.Add($"restore:{string.Join(",", files)}");
            return Task.CompletedTask;
        }

        public Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            StagedFiles.AddRange(files);
            Invocations.Add($"stage:{string.Join(",", files)}");
            return Task.CompletedTask;
        }

        public Task ResetStagingAsync(CancellationToken cancellationToken = default)
        {
            StagingReset = true;
            Invocations.Add("reset");
            return Task.CompletedTask;
        }
    }
}
