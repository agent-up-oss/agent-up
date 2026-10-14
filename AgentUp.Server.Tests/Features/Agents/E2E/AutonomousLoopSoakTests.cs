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
using AgentUp.Server.Tests.Features.Agents.Unit;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

[TestFixture]
[Category("LoopSoak")]
[CancelAfter(180_000)]
public sealed class AutonomousLoopSoakTests
{
    [Test]
    public async Task ThreeWorkspaces_drainHundredsOfAssignedIssuesWithoutSharingState()
    {
        var runs = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => DrainAsync(80)));

        Assert.Multiple(() =>
        {
            Assert.That(runs.Select(run => run.Prompts), Is.EqualTo(new[] { 80, 80, 80 }));
            Assert.That(runs.Select(run => run.Terminal), Is.EqualTo(new[] { LoopTerminal.Idle, LoopTerminal.Idle, LoopTerminal.Idle }));
        });
    }

    [Test]
    public async Task Restart_doesNotResumeAPersistedActiveLoop()
    {
        var host = await HostAsync();
        host.Settings.Save(host.Settings.Get(host.WorkspaceId) with
        {
            AutonomyActive = true,
            Loop = new LoopStatusSnapshot(true, "running", null, null, null, null, [])
        });

        host.Autonomy.ReconcileAfterRestart();

        Assert.Multiple(() =>
        {
            Assert.That(host.Autonomy.Status(host.WorkspaceId).Active, Is.False);
            Assert.That(host.Autonomy.Status(host.WorkspaceId).StopReason, Is.EqualTo("restarted"));
        });
        await host.DisposeAsync();
    }

    private static async Task<(int Prompts, LoopTerminal? Terminal)> DrainAsync(int issues)
    {
        var host = await HostAsync();
        await host.Workspaces.UpdateBoundAgentAsync(host.WorkspaceId, "codex");
        host.Settings.Save(host.Settings.Get(host.WorkspaceId) with
        {
            PolicyRules = [new PermissionRule("all", PermissionRuleKind.WorktreeWrite, true)],
            HostConnectionAuthorized = true
        });
        Assert.That(host.Autonomy.TryEnableServer(out _), Is.True);
        host.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
        host.Issues.Replace(host.WorkspaceId, Enumerable.Range(1, issues).Select(number =>
            new AssignedIssue(number.ToString(), number, $"Issue {number}", "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, $"https://example/{number}")).ToArray());

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (host.Autonomy.Status(host.WorkspaceId).Terminal is null && DateTime.UtcNow < deadline)
            await Task.Delay(20);

        var prompts = host.Process.Prompts.Count;
        var terminal = host.Autonomy.Status(host.WorkspaceId).Terminal;
        await host.DisposeAsync();
        return (prompts, terminal);
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
        var scheduling = new AgentSchedulingService(
            workspaces,
            new FakeAgentProcessFactory(process),
            new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Agents:Codex:Command"] = command }).Build()),
            new FakeSubscriptionLoginProvider(),
            new FakeProcessEnvironmentProvider(),
            new FakeClaudeCredentialStore(),
            new AgentSubscriptionAuth(),
            new AgentEventFrameProvider(),
            new AgentEventService(new AgentEventFrameProvider()),
            NullLogger<AgentSchedulingService>.Instance,
            settings: settings);
        var issues = new MemoryAssignedIssueQueue();
        var git = new FakeGitWorkingTreeProvider();
        var autonomy = new AutonomousWorkspaceService(
            new ConfiguredAutonomySettings(new ConfigurationBuilder().AddInMemoryCollection().Build(), settings),
            settings,
            new LoopPreconditionProvider(
                workspaces,
                new AgentUp.Server.Features.Git.Controllers.GitQueryController(
                    new AgentUp.Server.Features.Git.Services.GitChangeTreeService(
                        workspaces, git, new FakeWorkspacePromptGuard())),
                new AgentUp.Server.Features.Commits.Controllers.CommitsController(
                    new AgentUp.Server.Features.Commits.Services.CommitsService(
                        new EmptyQueue(),
                        new EmptyGit(),
                        new AgentUp.CommitPolicy.Features.CommitPolicy.Providers.CommitPolicyProvider())),
                settings,
                new AlwaysSupportedInspector()),
            issues,
            scheduling,
            new AutonomousLoopService(scheduling, new LoopInvariantGuard()),
            workspaces,
            new RoleToolFilterProvider());
        return new Host(workspace.Id, workspaces, settings, process, issues, autonomy, scheduling);
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

    private sealed class EmptyQueue : AgentUp.Server.Features.Commits.Interfaces.ICommitsQueueProvider
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

    private sealed class EmptyGit : AgentUp.Server.Features.Commits.Interfaces.ICommitsGitProvider
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
