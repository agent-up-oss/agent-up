using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AutonomousWorkspaceServiceTests
{
    [Test]
    public async Task ActivateAsync_refusesWhenTheServerIsNotEnabled()
    {
        var host = await HostAsync();

        var result = await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(result.Error, Does.Contain("not enabled"));
        await host.DisposeAsync();
    }

    [Test]
    public async Task ActivateAsync_refusesWhenPreconditionsAreUnmet()
    {
        var host = await HostAsync();
        host.Settings.Save(host.Settings.Get(host.WorkspaceId) with { HostConnectionAuthorized = true });
        Assert.That(host.Autonomy.TryEnableServer(out _), Is.True);

        var result = await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(result.Error, Does.Contain("not bound"));
        await host.DisposeAsync();
    }

    [Test]
    public async Task ActivateAsync_idlesWhenTheAssignedQueueIsEmpty()
    {
        var host = await HostAsync();
        await ReadyAsync(host);

        var result = await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal == LoopTerminal.Idle);

        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.Null);
            Assert.That(host.Autonomy.Status(host.WorkspaceId).StopReason, Is.EqualTo("queue_empty"));
        });
        await host.DisposeAsync();
    }

    [Test]
    public async Task ActivateAsync_completesAssignedIssuesInDeclaredOrder()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Issues.Replace(host.WorkspaceId, [
            new AssignedIssue("2", 2, "Second", "b", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "https://example/2"),
            new AssignedIssue("1", 1, "First", "a", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "https://example/1")
        ]);

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal == LoopTerminal.Idle);

        Assert.That(host.Process.Prompts.First(), Does.Contain("issue #1"));
        await host.DisposeAsync();
    }

    [Test]
    public async Task ReconcileAfterRestart_marksAnActiveLoopFailedInsteadOfResuming()
    {
        var host = await HostAsync();
        host.Settings.Save(host.Settings.Get(host.WorkspaceId) with
        {
            AutonomyActive = true,
            Loop = new LoopStatusSnapshot(true, "running", null, null, null, null, [])
        });

        host.Autonomy.ReconcileAfterRestart();
        var status = host.Autonomy.Status(host.WorkspaceId);

        Assert.Multiple(() =>
        {
            Assert.That(status.Active, Is.False);
            Assert.That(status.Terminal, Is.EqualTo(LoopTerminal.Failed));
            Assert.That(status.StopReason, Is.EqualTo("restarted"));
        });
        await host.DisposeAsync();
    }

    [Test]
    public async Task Deactivate_stopsARunningLoop()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Process.HoldPrompt = true;
        host.Issues.Replace(host.WorkspaceId, [
            new AssignedIssue("1", 1, "One", "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "https://example/1")
        ]);
        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Phase == "running");

        var stopped = host.Autonomy.Deactivate(host.WorkspaceId);

        Assert.Multiple(() =>
        {
            Assert.That(stopped.Error, Is.Null);
            Assert.That(host.Autonomy.Status(host.WorkspaceId).Terminal, Is.EqualTo(LoopTerminal.Cancelled));
        });
        await host.DisposeAsync();
    }

    private static async Task ReadyAsync(Host host)
    {
        await host.Workspaces.UpdateBoundAgentAsync(host.WorkspaceId, "codex");
        host.Settings.Save(host.Settings.Get(host.WorkspaceId) with
        {
            PolicyRules = [new PermissionRule("all", PermissionRuleKind.WorktreeWrite, true)],
            HostConnectionAuthorized = true
        });
        Assert.That(host.Autonomy.TryEnableServer(out _), Is.True);
        host.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
    }

    private static async Task<Host> HostAsync()
    {
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService()),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(
            ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
        var workspaces = new WorkspaceQueryController(registry);
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var process = new FakeAgentProcessProvider();
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Agents:Codex:Command"] = command }).Build());
        var events = new AgentEventService(new AgentEventFrameProvider());
        var scheduling = new AgentSchedulingService(
            workspaces,
            new FakeAgentProcessFactory(process),
            commands,
            new FakeSubscriptionLoginProvider(),
            new FakeProcessEnvironmentProvider(),
            new FakeClaudeCredentialStore(),
            new AgentSubscriptionAuth(),
            new AgentEventFrameProvider(),
            events,
            NullLogger<AgentSchedulingService>.Instance,
            settings: settings);
        var issues = new MemoryAssignedIssueQueue();
        var git = new FakeGitWorkingTreeProvider();
        var preconditions = new LoopPreconditionProvider(
            workspaces,
            new AgentUp.Server.Features.Git.Controllers.GitQueryController(
                new AgentUp.Server.Features.Git.Services.GitChangeTreeService(
                    workspaces, git, new FakeWorkspacePromptGuard())),
            Commits(git),
            settings,
            new AlwaysSupportedInspector());
        var autonomy = new AutonomousWorkspaceService(
            new ConfiguredAutonomySettings(new ConfigurationBuilder().AddInMemoryCollection().Build(), settings),
            settings,
            preconditions,
            issues,
            scheduling,
            new AutonomousLoopService(scheduling, new LoopInvariantGuard()),
            workspaces,
            new RoleToolFilterProvider());
        return new Host(workspace.Id, workspaces, settings, process, issues, autonomy, scheduling);
    }

    private static AgentUp.Server.Features.Commits.Controllers.CommitsController Commits(FakeGitWorkingTreeProvider git)
        => new(new AgentUp.Server.Features.Commits.Services.CommitsService(
            new EmptyCommitsQueue(),
            new EmptyCommitsGit(),
            new AgentUp.CommitPolicy.Features.CommitPolicy.Providers.CommitPolicyProvider()));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        Assert.That(condition(), Is.True);
    }

    private sealed record Host(
        string WorkspaceId,
        WorkspaceQueryController Workspaces,
        InMemoryAgentWorkspaceSettingsRepository Settings,
        FakeAgentProcessProvider Process,
        MemoryAssignedIssueQueue Issues,
        AutonomousWorkspaceService Autonomy,
        AgentSchedulingService Scheduling) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scheduling.DisposeAsync();
    }

    private sealed class AlwaysSupportedInspector : AgentUp.Server.Features.Agents.Interfaces.IEnabledAgentInspector
    {
        public bool SupportsPermissionRequests(string agentModuleId) => true;
    }

    private sealed class EmptyCommitsQueue : AgentUp.Server.Features.Commits.Interfaces.ICommitsQueueProvider
    {
        public Task<AgentUp.Server.Features.Commits.Models.CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(AgentUp.Server.Features.Commits.Models.CommitsQueue.Empty());

        public Task WriteAsync(string worktreePath, AgentUp.Server.Features.Commits.Models.CommitsQueue queue, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
            => operation(cancellationToken);
    }

    private sealed class EmptyCommitsGit : AgentUp.Server.Features.Commits.Interfaces.ICommitsGitProvider
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

        public Task<AgentUp.Server.Features.Commits.Models.GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
            => Task.FromResult(AgentUp.Server.Features.Commits.Models.GitOperationState.None);

        public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
