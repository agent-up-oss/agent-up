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

    private static async Task<(BoundWorkspaceCatalog Catalog, string BoundId)> CatalogAsync(
        string? queuePath = null)
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var primary = await registry.RegisterAsync(ServerDomain.Workspace().Build());
        await registry.RegisterAsync(ServerDomain.SecondWorkspace().Build());
        var queue = ServerDomain.Queue().InWorktree(queuePath).Build();
        var commits = new CommitsController(new CommitsService(
            new StaticQueueProvider(queue),
            new EmptyGitProvider(),
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
}
