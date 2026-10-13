using System.Text.Json;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AutonomousLoopServiceTests
{
    [Test]
    public async Task RunAsync_completesWhenTheAgentMarksTheTaskDone()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
            Assert.That(result.CompletedTask, Is.True);
            Assert.That(harness.Loop.Invariants(result), Is.Empty);
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_resolvesPermissionsThroughTheAttachedPolicy()
    {
        var harness = await HarnessAsync();
        harness.Process.BeforePromptCompletes = _ => harness.Process.RequestPermissionAsync(
            JsonSerializer.SerializeToElement(new
            {
                options = new[]
                {
                    new { optionId = "allow-once", kind = "allow_once" },
                    new { optionId = "reject-once", kind = "reject_once" }
                }
            }));
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.PermissionRuleIds, Is.EqualTo(new[] { "allow-all" }));
            Assert.That(result.PermissionsDrained, Is.True);
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_stopsAsBlockedPolicyWhenNoRuleMatches()
    {
        var harness = await HarnessAsync();
        harness.Process.BeforePromptCompletes = _ => harness.Process.RequestPermissionAsync(
            JsonSerializer.SerializeToElement(new { options = new[] { new { optionId = "reject-once" } } }));
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.BlockedPolicy));
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_stopsAtTheIterationCap()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id, maxIterations: 2), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.EndTurn));
            Assert.That(result.Iterations, Is.EqualTo(2));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_failsWhenTheWorkspaceAgentIsNotScheduled()
    {
        var harness = await HarnessAsync();

        var result = await harness.Loop.RunAsync(Request("missing-workspace"), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Failed));
            Assert.That(result.StopReason, Is.EqualTo("not-found"));
            Assert.That(result.BlockReason, Does.Contain("not scheduled"));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_failsWhenContextExceedsTheDeclaredLimit()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id, contextCharLimit: 1), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Failed));
            Assert.That(result.StopReason, Is.EqualTo("context_limit"));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_failsWhenThePromptReturnsAnError()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptFailure = new InvalidOperationException("agent crashed");
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Failed));
            Assert.That(result.BlockReason, Does.Contain("agent crashed"));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_stopsAsCancelledWhenTheAgentReportsIt()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "cancelled" });
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Cancelled));
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_returnsMaxIterationsWhenTheCapIsZero()
    {
        var harness = await HarnessAsync();
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id, maxIterations: 0), CancellationToken.None);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.MaxIterations));
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_stopsAsCancelledWhenTheCallerCancelsBeforeAPrompt()
    {
        var harness = await HarnessAsync();
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), cancelled.Token);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Cancelled));
        await harness.Scheduling.DisposeAsync();
    }

    private static LoopRunRequest Request(string workspaceId, int maxIterations = 4, int contextCharLimit = 100_000) => new(
        workspaceId,
        "finish the task",
        maxIterations,
        new AllowListedPermissionPolicy("allow-once", "allow-all"),
        contextCharLimit);

    private static async Task<Harness> HarnessAsync()
    {
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService()),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(
            ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
        var process = new FakeAgentProcessProvider();
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Agents:Codex:Command"] = command }).Build());
        var events = new AgentEventService(new AgentEventFrameProvider());
        var scheduling = new AgentSchedulingService(
            new WorkspaceQueryController(registry),
            new FakeAgentProcessFactory(process),
            commands,
            new FakeSubscriptionLoginProvider(),
            new FakeProcessEnvironmentProvider(),
            new FakeClaudeCredentialStore(),
            new AgentSubscriptionAuth(),
            new AgentEventFrameProvider(),
            events,
            NullLogger<AgentSchedulingService>.Instance);
        return new Harness(workspace, process, scheduling, new AutonomousLoopService(scheduling, new LoopInvariantGuard()));
    }

    private sealed record Harness(
        Workspace Workspace,
        FakeAgentProcessProvider Process,
        AgentSchedulingService Scheduling,
        AutonomousLoopService Loop);
}
