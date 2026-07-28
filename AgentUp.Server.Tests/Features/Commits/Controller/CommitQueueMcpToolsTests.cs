using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Services;

namespace AgentUp.Server.Tests.Features.Commits.Controller;

[TestFixture]
public sealed class CommitQueueMcpToolsTests
{
    private FakeCommitsQueueProvider _queue = null!;
    private FakeCommitsGitProvider _git = null!;
    private CommitQueueMcpTools _tools = null!;

    [SetUp]
    public void SetUp()
    {
        _queue = new FakeCommitsQueueProvider();
        _git = new FakeCommitsGitProvider();
        var controller = new CommitsController(new CommitsService(_queue, _git));
        _tools = new CommitQueueMcpTools(new CommitQueueMcpService(controller));
    }

    [Test]
    public async Task EnqueueCommit_ReturnsSuccess_WhenCommitIsEnqueued()
    {
        var result = await _tools.EnqueueCommit(
            "/repos/app",
            "feat/new-thing",
            "feat: add new thing",
            ["src/Thing.cs"],
            null,
            CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Message, Does.Contain("The tracked files have been restored to their pre-change state"));
        Assert.That(result.Message, Does.Contain("Do NOT re-apply or modify those files"));
        Assert.That(_queue.Stored!.Commits, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GuardCommits_BlocksNewWork_WhenQueueHasEntry()
    {
        await _tools.EnqueueCommit("/repos/app", "feat/s", "feat: m", ["a.cs"], null, CancellationToken.None);

        var result = await _tools.GuardCommits("/repos/app", CancellationToken.None);

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Message, Does.Contain("blocked starting new work"));
        var guard = (CommitGuardResult)result.Data!;
        Assert.That(guard.Blockers.Single(), Does.Contain("still queued"));
    }

    [Test]
    public async Task GetCommitChanges_ReturnsQueueAssignment()
    {
        _git.ModifiedFiles = ["queued.cs", "loose.cs"];
        await _tools.EnqueueCommit("/repos/app", "feat/s", "feat: m", ["queued.cs"], null, CancellationToken.None);

        var result = await _tools.GetCommitChanges("/repos/app", CancellationToken.None);

        Assert.That(result.Succeeded, Is.True);
        var changes = (CommitChangesResult)result.Data!;
        Assert.That(changes.QueuedFiles, Is.EqualTo(new[] { "queued.cs" }));
        Assert.That(changes.UnassignedFiles, Is.EqualTo(new[] { "loose.cs" }));
    }

    [Test]
    public async Task CommitMetadataTools_UpdateQueuedEntry()
    {
        await _tools.EnqueueCommit("/repos/app", "feat/s", "feat: m", ["a.cs"], null, CancellationToken.None);

        var message = await _tools.UpdateCommitMessage("/repos/app", "1", "fix: updated", CancellationToken.None);
        var tests = await _tools.UpdateCommitTests("/repos/app", "1", ["dotnet test"], CancellationToken.None);
        var files = await _tools.AddCommitFiles("/repos/app", "1", ["b.cs"], CancellationToken.None);

        Assert.That(message.Succeeded, Is.True);
        Assert.That(tests.Succeeded, Is.True);
        Assert.That(files.Succeeded, Is.True);
        Assert.That(_queue.Stored!.Commits[0].Message, Is.EqualTo("fix: updated"));
        Assert.That(_queue.Stored.Commits[0].Tests, Is.EqualTo(new[] { "dotnet test" }));
        Assert.That(_queue.Stored.Commits[0].Files, Is.EqualTo(new[] { "a.cs", "b.cs" }));
    }

    [Test]
    public async Task CommitArchiveTools_RemoveAndRestoreEntry()
    {
        await _tools.EnqueueCommit("/repos/app", "feat/s", "feat: m", ["a.cs"], null, CancellationToken.None);
        var entryId = _queue.Stored!.Commits[0].Id;

        var removed = await _tools.RemoveCommit("/repos/app", "1", CancellationToken.None);
        var restored = await _tools.RestoreCommit("/repos/app", entryId, CancellationToken.None);

        Assert.That(removed.Succeeded, Is.True);
        Assert.That(restored.Succeeded, Is.True);
        Assert.That(_queue.Stored.Commits.Single().Id, Is.EqualTo(entryId));
    }

    [Test]
    public async Task CommitEditTools_BeginAndAbortSession()
    {
        await _tools.EnqueueCommit("/repos/app", "feat/s", "feat: m", ["a.cs"], null, CancellationToken.None);

        var begin = await _tools.BeginCommitEdit("/repos/app", "1", CancellationToken.None);
        var abort = await _tools.AbortCommitEdit("/repos/app", CancellationToken.None);

        Assert.That(begin.Succeeded, Is.True);
        Assert.That(abort.Succeeded, Is.True);
        Assert.That(_git.PatchApplied, Is.True);
        Assert.That(_git.FilesRestored, Is.True);
        Assert.That(_queue.Stored!.ActiveSession, Is.Null);
    }

    [Test]
    public void EnqueueCommitDescription_TellsAgentsNotToUseGitDirectly()
    {
        var description = typeof(CommitQueueMcpTools)
            .GetMethod(nameof(CommitQueueMcpTools.EnqueueCommit))!
            .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
            .Cast<System.ComponentModel.DescriptionAttribute>()
            .Single()
            .Description;

        Assert.That(description, Does.Contain("Do NOT call git add"));
        Assert.That(description, Does.Contain("git commit"));
        Assert.That(description, Does.Contain("git stash"));
    }

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

    private sealed class FakeCommitsGitProvider : ICommitsGitProvider
    {
        public string[] ModifiedFiles { get; set; } = [];
        public bool PatchApplied { get; private set; }
        public bool FilesRestored { get; private set; }

        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(worktreePath);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>(ModifiedFiles);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult("diff --git a/a.cs b/a.cs\n");

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
        {
            PatchApplied = true;
            return Task.CompletedTask;
        }

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        {
            FilesRestored = true;
            return Task.CompletedTask;
        }
    }
}
