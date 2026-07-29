using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Services;

namespace AgentUp.Server.Tests.Features.Commits.Unit;

[TestFixture]
public sealed class CommitsServiceTests
{
    private const string WorktreePath = "/repo";

    [Test]
    public async Task EnqueueAsync_appendsEntryToEmptyQueue()
    {
        var queue = new FakeCommitsQueueProvider();
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("MySlice", "feat: add thing", ["a.cs"], []));

        Assert.That(result.Succeeded, Is.True);
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

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("Second", "fix: second", ["y.cs"], []));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(queue.Stored!.Commits, Has.Count.EqualTo(2));
        Assert.That(queue.Stored.Commits[1].Slice, Is.EqualTo("Second"));
    }

    [Test]
    public async Task EnqueueAsync_returnsQueueSize()
    {
        var queue = new FakeCommitsQueueProvider();
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("S", "chore: update queue", ["a.cs"], []));

        Assert.That(result.QueueSize, Is.EqualTo(1));
    }

    [Test]
    public async Task EnqueueAsync_messageWarnsAgentThatTrackedFilesWereRestored()
    {
        var queue = new FakeCommitsQueueProvider();
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("S", "chore: update queue", ["a.cs"], []));

        Assert.That(result.Message, Does.Contain("Enqueued 'S'. Queue size: 1."));
        Assert.That(result.Message, Does.Contain("The tracked files have been restored to their pre-change state"));
        Assert.That(result.Message, Does.Contain("Do NOT re-apply or modify those files"));
        Assert.That(result.Message, Does.Contain("the queue owns them now"));
    }

    [Test]
    public async Task EnqueueAsync_restoresFilesAfterCapturingPatch()
    {
        var queue = new FakeCommitsQueueProvider();
        var git = new FakeCommitsGitProvider();
        var service = new CommitsService(queue, git);

        await service.EnqueueAsync(WorktreePath, new EnqueueRequest("S", "chore: update queue", ["a.cs"], []));

        Assert.That(git.DiffRequested, Is.True);
        Assert.That(git.FilesRestored, Is.True);
    }

    [Test]
    public async Task EnqueueAsync_rollsBackQueueAndPatch_WhenRestoreFails()
    {
        var queue = new FakeCommitsQueueProvider();
        var git = new FakeCommitsGitProvider(restoreException: new IOException("restore failed"));
        var service = new CommitsService(queue, git);

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("S", "chore: update queue", ["a.cs"], []));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(queue.Stored!.Commits, Is.Empty);
        Assert.That(queue.Patches, Is.Empty);
    }

    [Test]
    public async Task EnqueueAsync_failsWhenActiveSessionExists()
    {
        var queue = new FakeCommitsQueueProvider(
            new CommitsQueue(2, [], new CommitEditSession("entry-1", "entry-1", ["a.cs"])));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("S", "chore: update queue", ["b.cs"], []));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("edit session"));
    }

    [Test]
    public async Task EnqueueAsync_failsWhenFileAlreadyAssignedToAnotherEntry()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("First", "fix: first", ["a.cs"], [], "entry-1")
        ]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("Second", "fix: second", ["a.cs"], []));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("a.cs"));
    }

    [Test]
    public async Task EnqueueAsync_failsWhenGitOperationIsActive()
    {
        var queue = new FakeCommitsQueueProvider();
        var git = new FakeCommitsGitProvider(operationState: new GitOperationState("merge", true));
        var service = new CommitsService(queue, git);

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("S", "chore: update queue", ["a.cs"], []));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("Git merge"));
        Assert.That(queue.Stored, Is.Null);
    }

    [Test]
    public async Task EnqueueAsync_rejectsUnsupportedConventionalCommitPrefix()
    {
        var service = new CommitsService(new FakeCommitsQueueProvider(), new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("Commits", "test(commits): cover queue", ["a.cs"], []));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("feat, fix, chore, refactor, style, docs"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsDocsCommitWithRuntimeFile()
    {
        var service = new CommitsService(new FakeCommitsQueueProvider(), new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("Commits", "docs(commits): explain queue", ["AgentUp.Server/Features/Commits/Services/CommitsService.cs"], []));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("docs commits may only include documentation files"));
    }

    [Test]
    public async Task EnqueueAsync_allowsDocsCommitWithDocumentationFiles()
    {
        var queue = new FakeCommitsQueueProvider();
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("guidance", "docs(commits): explain queue", ["docs/developer-guide/mcp.md", "AGENTS.md"], []));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(queue.Stored!.Commits.Single().Message, Is.EqualTo("docs(commits): explain queue"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsStyleCommitWithNonStyleFile()
    {
        var service = new CommitsService(new FakeCommitsQueueProvider(), new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("Commits", "style(ui): tune layout", ["AgentUp.Desktop/Features/Workspaces/ViewModels/MainViewModel.cs"], []));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("style commits may only include CSS or HTML files"));
    }

    [Test]
    public async Task EnqueueAsync_allowsStyleCommitWithCssAndHtmlFiles()
    {
        var queue = new FakeCommitsQueueProvider();
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("docs-style", "style(docs): tune layout", ["docs/src/css/custom.css", "docs/static/index.html"], []));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(queue.Stored!.Commits.Single().Files, Is.EqualTo(new[] { "docs/src/css/custom.css", "docs/static/index.html" }));
    }

    [Test]
    public async Task EnqueueAsync_failsWhenFilesSpanMultipleFeatureSlices()
    {
        var service = new CommitsService(new FakeCommitsQueueProvider(), new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest(
            "Commits",
            "fix: queue",
            [
                "AgentUp.Server/Features/Commits/Services/CommitsService.cs",
                "AgentUp.Server/Features/Workspaces/Services/WorkspaceRegistry.cs"
            ],
            []));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("multiple feature slices"));
    }

    [Test]
    public async Task EnqueueAsync_allowsSingleFeatureSliceWithMatchingTests()
    {
        var queue = new FakeCommitsQueueProvider();
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest(
            "fix/commits",
            "fix(commits): guard queue",
            [
                "AgentUp.Server/Features/Commits/Services/CommitsService.cs",
                "AgentUp.Server.Tests/Features/Commits/Unit/CommitsServiceTests.cs"
            ],
            []));

        Assert.That(result.Succeeded, Is.True);
        Assert.That(queue.Stored!.Commits.Single().Slice, Is.EqualTo("fix/commits"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsDuplicateReviewIssueId()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("Commits", "fix: first", ["a.cs"], [], "entry-1", "patch-1", "review-1")
        ]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("Commits", "fix: second", ["b.cs"], [], "review-1"));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("review-1"));
    }

    [Test]
    public async Task EnqueueAsync_rejectsDuplicateReviewIssueIdWithDifferentWhitespace()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("Commits", "fix: first", ["a.cs"], [], "entry-1", "patch-1", " review-1 ")
        ]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.EnqueueAsync(WorktreePath, new EnqueueRequest("Commits", "fix: second", ["b.cs"], [], "review-1"));

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("review-1"));
    }

    [Test]
    public async Task UpdateMessageAsync_rejectsDocsPrefixForRuntimeEntry()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("Commits", "fix(commits): validate prefix", ["AgentUp.Server/Features/Commits/Services/CommitsService.cs"], [], "entry-1")
        ]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.UpdateMessageAsync(WorktreePath, "1", "docs(commits): explain prefix");

        Assert.That(result.Success, Is.False);
        Assert.That(result.Message, Does.Contain("docs commits may only include documentation files"));
        Assert.That(queue.Stored!.Commits.Single().Message, Is.EqualTo("fix(commits): validate prefix"));
    }

    [Test]
    public async Task GetStatusAsync_returnsEmptyEntriesWhenQueueIsEmpty()
    {
        var service = new CommitsService(new FakeCommitsQueueProvider(), new FakeCommitsGitProvider());

        var result = await service.GetStatusAsync(WorktreePath);

        Assert.That(result.Entries, Is.Empty);
        Assert.That(result.UnassignedFiles, Is.Empty);
    }

    [Test]
    public async Task GetStatusAsync_returnsQueuedEntries()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("Slice", "msg", ["a.cs"], [])
        ]));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.GetStatusAsync(WorktreePath);

        Assert.That(result.Entries, Has.Count.EqualTo(1));
        Assert.That(result.Entries[0].Slice, Is.EqualTo("Slice"));
    }

    [Test]
    public async Task GetStatusAsync_flagsModifiedFilesNotInAnyEntry()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("Slice", "msg", ["owned.cs"], [])
        ]));
        var git = new FakeCommitsGitProvider(modifiedFiles: ["owned.cs", "unassigned.cs"]);
        var service = new CommitsService(queue, git);

        var result = await service.GetStatusAsync(WorktreePath);

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

        var result = await service.GetStatusAsync(WorktreePath);

        Assert.That(result.UnassignedFiles, Is.Empty);
    }

    [Test]
    public async Task GetStatusAsync_returnsActiveSession()
    {
        var session = new CommitEditSession("entry-1", "entry-1", ["a.cs"]);
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(2, [], session));
        var service = new CommitsService(queue, new FakeCommitsGitProvider());

        var result = await service.GetStatusAsync(WorktreePath);

        Assert.That(result.ActiveSession, Is.Not.Null);
        Assert.That(result.ActiveSession!.EntryId, Is.EqualTo("entry-1"));
    }

    // ── fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public CommitsQueue? Stored { get; private set; } = initial;
        public Dictionary<string, string> Patches { get; } = [];

        public Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored ?? CommitsQueue.Empty());

        public Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default)
        {
            Stored = queue;
            return Task.CompletedTask;
        }

        public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
        {
            Patches[patchKey] = patch;
            return Task.CompletedTask;
        }

        public Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
        {
            Patches.Remove(patchKey);
            return Task.CompletedTask;
        }

        public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult(Patches.GetValueOrDefault(patchKey));

        public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeCommitsGitProvider(
        string[]? modifiedFiles = null,
        Exception? restoreException = null,
        GitOperationState? operationState = null) : ICommitsGitProvider
    {
        public bool DiffRequested { get; private set; }
        public bool FilesRestored { get; private set; }

        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(modifiedFiles ?? []);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            DiffRequested = true;
            return Task.FromResult("diff --git a/a.cs b/a.cs\n");
        }

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(operationState ?? GitOperationState.None);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            if (restoreException is not null)
                throw restoreException;

            FilesRestored = true;
            return Task.CompletedTask;
        }
    }
}
