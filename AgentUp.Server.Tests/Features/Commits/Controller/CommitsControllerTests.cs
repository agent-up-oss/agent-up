using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Services;

namespace AgentUp.Server.Tests.Features.Commits.Controller;

[TestFixture]
public sealed class CommitsControllerTests
{
    private const string WorktreePath = "/repo";

    [Test]
    public async Task EnqueueAsync_delegatesToService()
    {
        var queue = new FakeCommitsQueueProvider();
        var controller = new CommitsController(new CommitsService(queue, new FakeCommitsGitProvider(), new CommitPolicyProvider()));
        var request = new EnqueueRequest("S", "refactor(S): update queue", ["a.cs"], []);

        var result = await controller.EnqueueAsync(WorktreePath, request);

        Assert.That(result.Succeeded, Is.True);
        Assert.That(queue.Stored!.Commits, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task GetStatusAsync_delegatesToService()
    {
        var queue = new FakeCommitsQueueProvider(new CommitsQueue(1, [
            new CommitEntry("Slice", "msg", ["a.cs"], [])
        ]));
        var controller = new CommitsController(new CommitsService(queue, new FakeCommitsGitProvider(), new CommitPolicyProvider()));

        var result = await controller.GetStatusAsync(WorktreePath);

        Assert.That(result.Entries, Has.Count.EqualTo(1));
    }

    // ── fakes ─────────────────────────────────────────────────────────────────

    private sealed class FakeCommitsQueueProvider(CommitsQueue? initial = null) : ICommitsQueueProvider
    {
        public CommitsQueue? Stored { get; private set; } = initial;

        public Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored ?? CommitsQueue.Empty());

        public Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default)
        {
            Stored = queue;
            return Task.CompletedTask;
        }

        public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class FakeCommitsGitProvider : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<string>>([]);

        public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(false);

        public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(GitOperationState.None);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
