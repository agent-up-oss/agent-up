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
        var git = new FakeCommitsGitProvider(["owned.cs", "unassigned.cs"]);
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
        var git = new FakeCommitsGitProvider(["a.cs", "b.cs"]);
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
    public async Task StageNextAsync_stagesHeadEntryAndDeletesQueueWhenNowEmpty()
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
        Assert.That(queue.Deleted, Is.True);
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
        var entry = new CommitEntry("Slice", "feat: msg", ["a.cs"], []);
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [entry]));
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        await service.StageNextAsync();

        Assert.That(git.StagingReset, Is.True);
    }

    [Test]
    public async Task EnqueueAsync_savesPatchForEntry()
    {
        var queue = new FakeCommitsQueueProvider();
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        await service.EnqueueAsync(new EnqueueRequest("MySlice", "feat: add thing", ["a.cs"], []));

        Assert.That(git.DiffRequested, Is.True);
    }

    [Test]
    public async Task ClearAsync_deletesQueue()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("S", "m", ["f.cs"], [])
        ]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        await service.ClearAsync();

        Assert.That(queue.Deleted, Is.True);
    }

    // ── fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public CommitsQueue? Stored { get; private set; } = initial;
        public bool Deleted { get; private set; }

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

        public Task SavePatchAsync(string slice, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeCommitsGitProvider(params string[] modifiedFiles) : ICommitsGitProvider
    {
        public List<string> StagedFiles { get; } = [];
        public bool StagingReset { get; private set; }

        public Task<string> GetRepoRootAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(modifiedFiles);

        public bool DiffRequested { get; private set; }

        public Task<string> GetDiffAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            DiffRequested = true;
            return Task.FromResult(string.Empty);
        }

        public Task<bool> HasStagedChangesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task StageFilesAsync(IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            StagedFiles.AddRange(files);
            return Task.CompletedTask;
        }

        public Task ResetStagingAsync(CancellationToken cancellationToken = default)
        {
            StagingReset = true;
            return Task.CompletedTask;
        }
    }
}
