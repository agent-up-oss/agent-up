using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Server.Features.Authentication.Providers;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class BoundWorkspaceCatalogTests
{
    [Test]
    public async Task PathTargetsWorkspaceAsync_AllowsTheBoundWorktreeAndRefusesAnotherWorkspace()
    {
        var (catalog, boundId) = await CatalogAsync();

        var own = await catalog.PathTargetsWorkspaceAsync(boundId, ServerDomain.WorktreePath, CancellationToken.None);
        var other = await catalog.PathTargetsWorkspaceAsync(boundId, ServerDomain.SecondWorktreePath, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(own, Is.True);
            Assert.That(other, Is.False);
        });
    }

    [Test]
    public async Task PathTargetsWorkspaceAsync_AllowsTheManagedQueueWorktreeOfTheBoundWorkspace()
    {
        var (catalog, boundId) = await CatalogAsync(queuePath: "/managed/queue");

        var allowed = await catalog.PathTargetsWorkspaceAsync(boundId, "/managed/queue", CancellationToken.None);

        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task PathTargetsWorkspaceAsync_ReturnsFalseWhenTheWorkspaceIsUnknown()
    {
        var (catalog, _) = await CatalogAsync();

        var allowed = await catalog.PathTargetsWorkspaceAsync("missing", ServerDomain.WorktreePath, CancellationToken.None);

        Assert.That(allowed, Is.False);
    }

    [Test]
    public async Task PathTargetsWorkspaceAsync_TreatsEquivalentRootedPathsAsTheSameWorktree()
    {
        var (catalog, boundId) = await CatalogAsync();

        var allowed = await catalog.PathTargetsWorkspaceAsync(boundId, "/repo/./primary", CancellationToken.None);

        Assert.That(allowed, Is.True);
    }

    [Test]
    public async Task PathTargetsWorkspaceAsync_RefusesARelativePath()
    {
        var (catalog, boundId) = await CatalogAsync();

        var allowed = await catalog.PathTargetsWorkspaceAsync(boundId, "primary", CancellationToken.None);

        Assert.That(allowed, Is.False);
    }

    [Test]
    public async Task PathTargetsWorkspaceAsync_RefusesAnInvalidRootedPath()
    {
        var (catalog, boundId) = await CatalogAsync();

        var allowed = await catalog.PathTargetsWorkspaceAsync(boundId, "/\0", CancellationToken.None);

        Assert.That(allowed, Is.False);
    }

    [Test]
    public async Task PathTargetsWorkspaceAsync_TreatsQueueLookupFailuresAsNotMatching()
    {
        var (catalog, boundId) = await CatalogAsync(gitError: new IOException("queue unavailable"));

        var allowed = await catalog.PathTargetsWorkspaceAsync(boundId, "/elsewhere", CancellationToken.None);

        Assert.That(allowed, Is.False);
    }

    private static async Task<(BoundWorkspaceCatalog Catalog, string BoundId)> CatalogAsync(
        string? queuePath = null,
        Exception? gitError = null)
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var primary = await registry.RegisterAsync(ServerDomain.Workspace().Build());
        await registry.RegisterAsync(ServerDomain.SecondWorkspace().Build());
        var queue = ServerDomain.Queue().InWorktree(queuePath).Build();
        var commits = new CommitsController(new CommitsService(
            new StaticQueueProvider(queue),
            gitError is null ? new EmptyGitProvider() : new ThrowingGitProvider(gitError),
            new CommitPolicyProvider()));
        return (new BoundWorkspaceCatalog(new WorkspaceQueryController(registry), commits), primary.Id);
    }

    private sealed class StaticQueueProvider(CommitsQueue stored) : ICommitsQueueProvider
    {
        public Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(stored);

        public Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class EmptyGitProvider : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(worktreePath);

        public Task<string> GetRepositoryIdentityAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(worktreePath);

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

    private sealed class ThrowingGitProvider(Exception error) : ICommitsGitProvider
    {
        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(worktreePath);

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromException<IReadOnlyList<string>>(error);

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
