using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using ModelContextProtocol.Server;
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
            ServerDomain.Issue().Numbered(2).Titled("Second").WithBody("b").Build(),
            ServerDomain.Issue().Numbered(1).Titled("First").WithBody("a").Build()
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
    public async Task Setters_returnNotFoundForAnUnknownWorkspace()
    {
        var host = await HostAsync();
        var hostConnection = await host.Autonomy.SetHostConnectionAsync("missing", true);
        var policy = await host.Autonomy.SetPolicyAsync("missing", []);
        var role = await host.Autonomy.SetRoleAsync("missing", null);
        var order = await host.Autonomy.SetQueueOrderAsync("missing", AssignedIssueOrder.NumberAsc);
        var activate = await host.Autonomy.ActivateAsync("missing", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(hostConnection.Found, Is.False);
            Assert.That(policy.Found, Is.False);
            Assert.That(role.Found, Is.False);
            Assert.That(order.Found, Is.False);
            Assert.That(activate.Found, Is.False);
            Assert.That(host.Autonomy.Deactivate("missing").Found, Is.False);
        });
        await host.DisposeAsync();
    }

    [Test]
    public async Task SetQueueOrderAndRole_persistOnAKnownWorkspace()
    {
        var host = await HostAsync();
        await host.Autonomy.SetQueueOrderAsync(host.WorkspaceId, AssignedIssueOrder.UpdatedAsc);
        await host.Autonomy.SetRoleAsync(
            host.WorkspaceId,
            new AgentRoleDefinition("Ship", ["read_file"], RoleTriggerKind.IssueAssigned, RoleTerminationKind.ProposalOpened));
        host.Autonomy.RestrictRoleTools(new McpServerOptions(), host.WorkspaceId);
        host.Autonomy.RestrictRoleTools(new McpServerOptions(), null);
        host.Autonomy.DisableServer();

        Assert.Multiple(() =>
        {
            Assert.That(host.Settings.Get(host.WorkspaceId).QueueOrder, Is.EqualTo(AssignedIssueOrder.UpdatedAsc));
            Assert.That(host.Autonomy.RoleTools(host.WorkspaceId), Is.EqualTo(new[] { "read_file" }));
            Assert.That(host.Autonomy.ServerEnabled, Is.False);
        });
        await host.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_blocksWhenTheHostQueueThrows()
    {
        var host = await HostAsync(new ThrowingAssignedIssueQueue());
        await ReadyAsync(host);

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal == LoopTerminal.BlockedHostUnavailable);

        Assert.That(host.Autonomy.Status(host.WorkspaceId).StopReason, Is.EqualTo("host_unavailable"));
        await host.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_blocksWhenScheduleFails()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        await host.Workspaces.UpdateBoundAgentAsync(host.WorkspaceId, "cursor");
        host.Issues.Replace(host.WorkspaceId, [ServerDomain.Issue().Build()]);

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal == LoopTerminal.Failed);

        Assert.That(host.Autonomy.Status(host.WorkspaceId).StopReason, Is.EqualTo("schedule"));
        await host.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_blocksWhenPreconditionsFailOnTheNextTask()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var host = await HostAsync(new ClearingAssignedIssueQueue(settings), settings);
        await ReadyAsync(host);
        host.Issues.Replace(host.WorkspaceId, [
            ServerDomain.Issue().Numbered(1).Build(),
            ServerDomain.Issue().Numbered(2).Titled("Two").Build()
        ]);

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal == LoopTerminal.BlockedPrecondition);

        Assert.That(host.Autonomy.Status(host.WorkspaceId).StopReason, Is.EqualTo("precondition"));
        await host.DisposeAsync();
    }

    [Test]
    public async Task ActivateAsync_replacesAPreviousRun()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Process.HoldPrompt = true;
        host.Issues.Replace(host.WorkspaceId, [ServerDomain.Issue().Build()]);
        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Phase == "running");

        var second = await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);

        Assert.That(second.Error, Is.Null);
        await host.DisposeAsync();
    }

    [Test]
    public async Task Deactivate_stopsARunningLoop()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Process.HoldPrompt = true;
        host.Issues.Replace(host.WorkspaceId, [ServerDomain.Issue().Build()]);
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

    [Test]
    public async Task ActivateAsync_drainsProposalsWhenTheRoleTriggerIsProposalAwaitingReview()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        await host.Autonomy.SetRoleAsync(
            host.WorkspaceId,
            new AgentRoleDefinition("Review", [], RoleTriggerKind.ProposalAwaitingReview, RoleTerminationKind.ReviewSubmitted));
        host.Progress.Record(host.WorkspaceId, RoleTerminationKind.ReviewSubmitted);
        host.Proposals.Replace(host.WorkspaceId, [
            new AssignedProposal("p1", "Open the review", "body", DateTimeOffset.UnixEpoch, "https://example/p1")
        ]);

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal == LoopTerminal.Idle);

        Assert.That(host.Process.Prompts.First(), Does.Contain("Open the review"));
        await host.DisposeAsync();
    }

    [Test]
    public async Task ActivateAsync_blocksWhenRoleTerminationIsUnmetAfterCompleted()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        await host.Autonomy.SetRoleAsync(
            host.WorkspaceId,
            new AgentRoleDefinition("Ship", [], RoleTriggerKind.IssueAssigned, RoleTerminationKind.ProposalOpened));
        host.Issues.Replace(host.WorkspaceId, [ServerDomain.Issue().Build()]);

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal == LoopTerminal.BlockedAwaitingInput);

        Assert.That(host.Autonomy.Status(host.WorkspaceId).StopReason, Is.EqualTo("termination"));
        await host.DisposeAsync();
    }

    [Test]
    public async Task ActivateAsync_skipsAnIssueTheHostQueueKeepsReturning()
    {
        var host = await HostAsync(new StickyAssignedIssueQueue());
        await ReadyAsync(host);
        host.Issues.Replace(host.WorkspaceId, [ServerDomain.Issue().Build()]);

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal == LoopTerminal.Idle);

        Assert.That(host.Process.Prompts, Has.Count.EqualTo(1));
        await host.DisposeAsync();
    }

    [Test]
    public async Task ActivateAsync_keepsTheOriginalTaskWhenSteeringArrives()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Process.HoldPrompt = true;
        host.Issues.Replace(host.WorkspaceId, [ServerDomain.Issue().Titled("Keep this task").Build()]);
        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Process.Prompts.Count > 0);

        await host.Scheduling.PromptAsync(host.WorkspaceId, "Rewrite this as a different task", CancellationToken.None);
        host.Process.HoldPrompt = false;
        host.Process.CompletePrompt();
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Terminal is not null);

        Assert.Multiple(() =>
        {
            Assert.That(host.Process.Prompts.First(), Does.Contain("Keep this task"));
            Assert.That(host.Process.Prompts.Any(prompt => prompt.Contains("do not rewrite the task", StringComparison.Ordinal)), Is.True);
        });
        await host.DisposeAsync();
    }

    [Test]
    public async Task ReconcileAfterRestart_cancelsALiveRun()
    {
        var host = await HostAsync();
        await ReadyAsync(host);
        host.Process.HoldPrompt = true;
        host.Issues.Replace(host.WorkspaceId, [ServerDomain.Issue().Build()]);
        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(host.WorkspaceId).Phase == "running");

        host.Autonomy.ReconcileAfterRestart();

        Assert.Multiple(() =>
        {
            Assert.That(host.Autonomy.Status(host.WorkspaceId).Active, Is.False);
            Assert.That(host.Autonomy.Status(host.WorkspaceId).StopReason, Is.EqualTo("restarted"));
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

    private static async Task<Host> HostAsync(
        IAssignedIssueQueue? issues = null,
        InMemoryAgentWorkspaceSettingsRepository? settings = null)
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
        settings ??= new InMemoryAgentWorkspaceSettingsRepository();
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
        issues ??= new MemoryAssignedIssueQueue();
        var proposals = new MemoryAssignedProposalQueue();
        var progress = new MemoryRoleProgress();
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
            proposals,
            progress,
            scheduling,
            new AutonomousLoopService(scheduling, new LoopInvariantGuard(), new ImmediateLoopBackoff()),
            workspaces,
            new RoleToolFilterProvider());
        return new Host(workspace.Id, workspaces, settings, process, issues, proposals, progress, autonomy, scheduling);
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
        IAssignedIssueQueue Issues,
        MemoryAssignedProposalQueue Proposals,
        MemoryRoleProgress Progress,
        AutonomousWorkspaceService Autonomy,
        AgentSchedulingService Scheduling) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scheduling.DisposeAsync();
    }

    private sealed class StickyAssignedIssueQueue : IAssignedIssueQueue
    {
        private IReadOnlyList<AssignedIssue> _items = [];

        public Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
            string workspaceId,
            AssignedIssueOrder order,
            CancellationToken cancellationToken) =>
            Task.FromResult(_items);

        public void Replace(string workspaceId, IReadOnlyList<AssignedIssue> issues)
        {
            if (_items.Count == 0)
                _items = issues;
        }
    }

    private sealed class ThrowingAssignedIssueQueue : IAssignedIssueQueue
    {
        public Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
            string workspaceId,
            AssignedIssueOrder order,
            CancellationToken cancellationToken)
            => throw new HttpRequestException("GitHub is unavailable.");

        public void Replace(string workspaceId, IReadOnlyList<AssignedIssue> issues)
        {
        }
    }

    private sealed class ClearingAssignedIssueQueue(InMemoryAgentWorkspaceSettingsRepository settings) : IAssignedIssueQueue
    {
        private IReadOnlyList<AssignedIssue> _items = [];

        public Task<IReadOnlyList<AssignedIssue>> ListAssignedAsync(
            string workspaceId,
            AssignedIssueOrder order,
            CancellationToken cancellationToken)
            => Task.FromResult(_items);

        public void Replace(string workspaceId, IReadOnlyList<AssignedIssue> issues)
        {
            var draining = _items.Count > 0 && issues.Count < _items.Count;
            _items = issues;
            if (draining)
                settings.Save(settings.Get(workspaceId) with { PolicyRules = [] });
        }
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
