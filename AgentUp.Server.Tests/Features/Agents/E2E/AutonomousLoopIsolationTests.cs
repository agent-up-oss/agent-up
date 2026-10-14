using System.Text.Json;
using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Commits.Services;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Git.Services;
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
public sealed class AutonomousLoopIsolationTests
{
    [Test]
    public async Task ThreeWorkspacesOnOneServer_keepQueuesAndPoliciesIsolated()
    {
        var host = await HostAsync(() => new ScriptedAcpProcess(ScriptedTurnKind.Permission, ScriptedTurnKind.Complete));
        var first = await ReadyAsync(host, ServerDomain.Workspace().Named("Alpha").At("/repo/alpha").Build(), 1, PermissionRuleKind.WorktreeWrite);
        var second = await ReadyAsync(host, ServerDomain.SecondWorkspace().Build(), 10, PermissionRuleKind.Network);
        var third = await ReadyAsync(host, ServerDomain.Workspace().Named("Gamma").At("/repo/gamma").Build(), 20, PermissionRuleKind.WorktreeWrite);

        await Task.WhenAll(
            host.Autonomy.ActivateAsync(first, CancellationToken.None),
            host.Autonomy.ActivateAsync(second, CancellationToken.None),
            host.Autonomy.ActivateAsync(third, CancellationToken.None));
        await WaitUntilAsync(() =>
            host.Autonomy.Status(first).Terminal is not null
            && host.Autonomy.Status(second).Terminal is not null
            && host.Autonomy.Status(third).Terminal is not null, host, first, second, third);

        Assert.Multiple(() =>
        {
            Assert.That(host.Autonomy.Status(first).Terminal, Is.EqualTo(LoopTerminal.Idle));
            Assert.That(host.Autonomy.Status(second).Terminal, Is.EqualTo(LoopTerminal.BlockedPolicy));
            Assert.That(host.Autonomy.Status(third).Terminal, Is.EqualTo(LoopTerminal.Idle));
            Assert.That(host.Processes.Created.Any(process =>
                process.Prompts.Any(prompt => prompt.Contains("issue #1:", StringComparison.Ordinal))
                && process.Prompts.Any(prompt => prompt.Contains("issue #10:", StringComparison.Ordinal))), Is.False);
            Assert.That(host.Scheduling.Get(first)!.SessionId, Is.Not.EqualTo(host.Scheduling.Get(second)!.SessionId));
        });
        await host.DisposeAsync();
    }

    [Test]
    public async Task CrashedWorkspace_doesNotStallTheOthers()
    {
        var turns = new Queue<ScriptedTurnKind>([ScriptedTurnKind.Crash, ScriptedTurnKind.Complete, ScriptedTurnKind.Complete]);
        var host = await HostAsync(() => new ScriptedAcpProcess(turns.Dequeue()));
        var crashed = await ReadyAsync(host, ServerDomain.Workspace().Named("Crash").At("/repo/crash").Build(), 1);
        var healthy = await ReadyAsync(host, ServerDomain.SecondWorkspace().Build(), 2);
        var other = await ReadyAsync(host, ServerDomain.Workspace().Named("Other").At("/repo/other").Build(), 3);

        await host.Autonomy.ActivateAsync(crashed, CancellationToken.None);
        await host.Autonomy.ActivateAsync(healthy, CancellationToken.None);
        await host.Autonomy.ActivateAsync(other, CancellationToken.None);
        await WaitUntilAsync(() =>
            host.Autonomy.Status(crashed).Terminal is not null
            && host.Autonomy.Status(healthy).Terminal is not null
            && host.Autonomy.Status(other).Terminal is not null, host, crashed, healthy, other);

        Assert.Multiple(() =>
        {
            Assert.That(host.Autonomy.Status(crashed).Terminal, Is.EqualTo(LoopTerminal.Failed));
            Assert.That(host.Autonomy.Status(healthy).Terminal, Is.EqualTo(LoopTerminal.Idle));
            Assert.That(host.Autonomy.Status(other).Terminal, Is.EqualTo(LoopTerminal.Idle));
        });
        await host.DisposeAsync();
    }

    [Test]
    public async Task ConcurrentActivate_keepsOneSessionPerWorkspace()
    {
        var host = await HostAsync(() => new ScriptedAcpProcess(ScriptedTurnKind.Complete));
        var workspaceId = await ReadyAsync(host, ServerDomain.Workspace().Build(), 1);

        await Task.WhenAll(
            host.Autonomy.ActivateAsync(workspaceId, CancellationToken.None),
            host.Autonomy.ActivateAsync(workspaceId, CancellationToken.None));
        await WaitUntilAsync(() => host.Autonomy.Status(workspaceId).Terminal is not null, host, workspaceId);

        Assert.That(host.Scheduling.Get(workspaceId)!.SessionId, Is.Not.Null);
        await host.DisposeAsync();
    }

    [Test]
    public async Task RestartMidTask_marksFailedAndDoesNotResume()
    {
        var host = await HostAsync(() => new ScriptedAcpProcess(ScriptedTurnKind.HoldPipe));
        var workspaceId = await ReadyAsync(host, ServerDomain.Workspace().Build(), 1);
        await host.Autonomy.ActivateAsync(workspaceId, CancellationToken.None);
        await WaitUntilAsync(() => host.Autonomy.Status(workspaceId).Phase == "running", host, workspaceId);

        host.Autonomy.ReconcileAfterRestart();

        Assert.Multiple(() =>
        {
            Assert.That(host.Autonomy.Status(workspaceId).Active, Is.False);
            Assert.That(host.Autonomy.Status(workspaceId).StopReason, Is.EqualTo("restarted"));
            Assert.That(host.Autonomy.Status(workspaceId).Terminal, Is.EqualTo(LoopTerminal.Failed));
        });
        await host.DisposeAsync();
    }

    private static async Task<string> ReadyAsync(
        Host host,
        AgentUp.Server.Features.Workspaces.DTOs.RegisterWorkspaceRequest request,
        int issueNumber,
        PermissionRuleKind policy = PermissionRuleKind.WorktreeWrite)
    {
        var workspace = await host.Workspaces.RegisterAsync(request);
        await host.Workspaces.UpdateBoundAgentAsync(workspace.Id, "codex");
        host.Settings.Save(host.Settings.Get(workspace.Id) with
        {
            PolicyRules = [new PermissionRule("rule", policy, true)],
            HostConnectionAuthorized = true
        });
        host.Issues.Replace(workspace.Id, [ServerDomain.Issue().Numbered(issueNumber).Build()]);
        Assert.That(host.Autonomy.TryEnableServer(out _), Is.True);
        return workspace.Id;
    }

    private static async Task<Host> HostAsync(Func<ScriptedAcpProcess> createProcess)
    {
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService()),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        var workspaces = new WorkspaceQueryController(registry);
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var factory = new ScriptedProcessFactory(createProcess);
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var scheduling = new AgentSchedulingService(
            workspaces,
            factory,
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
                new GitQueryController(new GitChangeTreeService(workspaces, git, new FakeWorkspacePromptGuard())),
                new CommitsController(new CommitsService(new EmptyCommitsQueue(), new EmptyCommitsGit(), new CommitPolicyProvider())),
                settings,
                new AlwaysSupportedInspector()),
            issues,
            new MemoryAssignedProposalQueue(),
            new MemoryRoleProgress(),
            scheduling,
            new AutonomousLoopService(scheduling, new LoopInvariantGuard(), new ImmediateLoopBackoff()),
            workspaces,
            new RoleToolFilterProvider());
        return new Host(workspaces, settings, issues, factory, autonomy, scheduling);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, Host host, params string[] workspaceIds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        if (condition())
            return;
        foreach (var workspaceId in workspaceIds)
        {
            var status = host.Autonomy.Status(workspaceId);
            TestContext.Out.WriteLine($"{workspaceId} phase={status.Phase} terminal={status.Terminal} stop={status.StopReason} block={status.BlockReason}");
        }

        foreach (var process in host.Processes.Created)
        {
            TestContext.Out.WriteLine($"scripted prompts={process.Prompts.Count}");
            foreach (var prompt in process.Prompts)
                TestContext.Out.WriteLine(prompt);
        }

        Assert.Fail("Timed out waiting for isolated loop terminals.");
    }

    private sealed record Host(
        WorkspaceQueryController Workspaces,
        InMemoryAgentWorkspaceSettingsRepository Settings,
        MemoryAssignedIssueQueue Issues,
        ScriptedProcessFactory Processes,
        AutonomousWorkspaceService Autonomy,
        AgentSchedulingService Scheduling) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scheduling.DisposeAsync();
    }
}
