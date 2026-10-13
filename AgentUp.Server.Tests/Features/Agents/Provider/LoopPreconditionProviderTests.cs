using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Git.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class LoopPreconditionProviderTests
{
    [Test]
    public async Task UnmetAsync_namesAMissingBoundAgent()
    {
        var host = await HostAsync();

        var unmet = await host.Preconditions.UnmetAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(unmet, Does.Contain("not bound"));
    }

    [Test]
    public async Task UnmetAsync_namesAMissingPolicyAndHostConnection()
    {
        var host = await HostAsync();
        await host.Workspaces.UpdateBoundAgentAsync(host.WorkspaceId, "codex");

        var unmet = await host.Preconditions.UnmetAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(unmet, Does.Contain("permission policy"));
    }

    [Test]
    public async Task UnmetAsync_refusesAnAgentThatDoesNotSupportPermissionRequests()
    {
        var host = await HostAsync(supportsPermissions: false);
        await host.Workspaces.UpdateBoundAgentAsync(host.WorkspaceId, "codex");

        var unmet = await host.Preconditions.UnmetAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(unmet, Does.Contain("permission requests"));
    }

    [Test]
    public async Task UnmetAsync_refusesADetachedHead()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Git.Branch = "not on a git branch";

        var unmet = await host.Preconditions.UnmetAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(unmet, Does.Contain("git branch"));
    }

    [Test]
    public async Task UnmetAsync_refusesUnpushedCommits()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Git.Ahead = 2;

        var unmet = await host.Preconditions.UnmetAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(unmet, Does.Contain("unpushed"));
    }

    [Test]
    public async Task UnmetAsync_refusesAnActiveEditSession()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Queue.Stored = CommitsQueue.Empty() with
        {
            ActiveSession = new CommitEditSession("entry", "patch", ["a.cs"])
        };

        var unmet = await host.Preconditions.UnmetAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(unmet, Does.Contain("edit session"));
    }

    [Test]
    public async Task UnmetAsync_refusesUnassignedWorkingTreeChanges()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Git.Changes.Add(new GitChangeEntry("src/a.cs", GitChangeStatus.Modified));
        host.CommitsGit.Modified = ["src/a.cs"];

        var unmet = await host.Preconditions.UnmetAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(unmet, Does.Contain("not assigned"));
    }

    [Test]
    public async Task UnmetAsync_returnsNullWhenEveryPreconditionHolds()
    {
        var host = await HostAsync();
        await ReadyAsync(host);

        Assert.That(await host.Preconditions.UnmetAsync(host.WorkspaceId, CancellationToken.None), Is.Null);
    }

    private static async Task ReadyAsync(Host host)
    {
        await host.Workspaces.UpdateBoundAgentAsync(host.WorkspaceId, "codex");
        host.Settings.Save(host.Settings.Get(host.WorkspaceId) with
        {
            PolicyRules = [new PermissionRule("all", PermissionRuleKind.WorktreeWrite, true)],
            HostConnectionAuthorized = true
        });
    }

    private static async Task<Host> HostAsync(bool supportsPermissions = true)
    {
        var registry = ServerTestComposition.CreateRegistry();
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(
            ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
        var workspaces = new WorkspaceQueryController(registry);
        var git = new FakeGitWorkingTreeProvider();
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var queue = new MemoryCommitsQueue();
        var commitsGit = new MemoryCommitsGit();
        var commits = new CommitsController(new CommitsService(queue, commitsGit, new CommitPolicyProvider()));
        var preconditions = new LoopPreconditionProvider(
            workspaces,
            new GitQueryController(new GitChangeTreeService(workspaces, git, new FakeWorkspacePromptGuard())),
            commits,
            settings,
            new FixedInspector(supportsPermissions));
        return new Host(workspace.Id, workspaces, git, settings, queue, commitsGit, preconditions);
    }

    private sealed record Host(
        string WorkspaceId,
        WorkspaceQueryController Workspaces,
        FakeGitWorkingTreeProvider Git,
        InMemoryAgentWorkspaceSettingsRepository Settings,
        MemoryCommitsQueue Queue,
        MemoryCommitsGit CommitsGit,
        LoopPreconditionProvider Preconditions);

    private sealed class FixedInspector(bool supported) : IEnabledAgentInspector
    {
        public bool SupportsPermissionRequests(string agentModuleId) => supported;
    }

    private sealed class MemoryCommitsQueue : ICommitsQueueProvider
    {
        public CommitsQueue Stored { get; set; } = CommitsQueue.Empty();

        public Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Stored);

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

    private sealed class MemoryCommitsGit : ICommitsGitProvider
    {
        public IReadOnlyList<string> Modified { get; set; } = [];

        public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult("/repo");

        public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Modified);

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
