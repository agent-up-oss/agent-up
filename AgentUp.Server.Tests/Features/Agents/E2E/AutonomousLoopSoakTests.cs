using System.Diagnostics;
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
[Category("LoopSoak")]
[CancelAfter(180_000)]
public sealed class AutonomousLoopSoakTests
{
    [Test]
    public async Task TwoHundredScriptedTasks_stayInsideResourceBounds()
    {
        var process = Process.GetCurrentProcess();
        var beforeWorkingSet = process.WorkingSet64;
        var beforeHandles = process.HandleCount;
        var beforeThreads = process.Threads.Count;
        var host = await HostAsync();
        await host.Workspaces.UpdateBoundAgentAsync(host.WorkspaceId, "codex");
        host.Settings.Save(host.Settings.Get(host.WorkspaceId) with
        {
            PolicyRules = [new PermissionRule("all", PermissionRuleKind.WorktreeWrite, true)],
            HostConnectionAuthorized = true
        });
        Assert.That(host.Autonomy.TryEnableServer(out _), Is.True);
        host.Issues.Replace(host.WorkspaceId, Enumerable.Range(1, 200).Select(number =>
            ServerDomain.Issue().Numbered(number).Build()).ToArray());

        await host.Autonomy.ActivateAsync(host.WorkspaceId, CancellationToken.None);
        await WaitUntilAsync(host, () => host.Autonomy.Status(host.WorkspaceId).Terminal is not null);

        process.Refresh();
        var inspection = host.Scheduling.InspectLoop(host.WorkspaceId);
        Assert.Multiple(() =>
        {
            Assert.That(host.Autonomy.Status(host.WorkspaceId).Terminal, Is.EqualTo(LoopTerminal.Idle));
            Assert.That(host.Process.Prompts, Has.Count.EqualTo(200));
            Assert.That(inspection.TranscriptEvents, Is.LessThanOrEqualTo(LoopInvariantGuard.MaxTranscriptEvents));
            Assert.That(process.WorkingSet64, Is.LessThan(beforeWorkingSet + 256L * 1024 * 1024));
            Assert.That(process.HandleCount, Is.LessThan(beforeHandles + 512));
            Assert.That(process.Threads.Count, Is.LessThan(beforeThreads + 64));
        });
        await host.DisposeAsync();
    }

    private static async Task WaitUntilAsync(Host host, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(20);
        if (condition())
            return;
        var status = host.Autonomy.Status(host.WorkspaceId);
        TestContext.Out.WriteLine($"soak phase={status.Phase} terminal={status.Terminal} stop={status.StopReason} block={status.BlockReason}");
        TestContext.Out.WriteLine($"prompts={host.Process.Prompts.Count}");
        foreach (var prompt in host.Process.Prompts.Take(5))
            TestContext.Out.WriteLine(prompt);
        Assert.Fail("Soak loop did not reach a terminal. Transcript attached above.");
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
        var process = new ScriptedAcpProcess();
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
        return new Host(workspace.Id, workspaces, settings, process, issues, autonomy, scheduling);
    }

    private sealed record Host(
        string WorkspaceId,
        WorkspaceQueryController Workspaces,
        InMemoryAgentWorkspaceSettingsRepository Settings,
        ScriptedAcpProcess Process,
        MemoryAssignedIssueQueue Issues,
        AutonomousWorkspaceService Autonomy,
        AgentSchedulingService Scheduling) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Scheduling.DisposeAsync();
    }
}
