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
    public async Task RunAsync_stopsAsBlockedAwaitingInputAfterStandingPolicyReplies()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(
            Request(harness.Workspace.Id, maxIterations: 4) with { StandingPolicyReplies = 1 },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.BlockedAwaitingInput));
            Assert.That(result.BlockReason, Does.Contain("awaiting-input"));
            Assert.That(harness.Loop.Invariants(result), Is.Empty);
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_stopsAsBlockedRateLimitedAfterBackoffRetries()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "rate_limited" });
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.BlockedRateLimited));
            Assert.That(harness.Backoff.Attempts, Is.EqualTo(new[] { 1 }));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_completesAfterARateLimitBackoff()
    {
        var harness = await HarnessAsync();
        harness.Process.BeforePromptCompletes = _ =>
        {
            harness.Process.PromptResult = harness.Process.Prompts.Count == 1
                ? JsonSerializer.SerializeToElement(new { stopReason = "rate_limited" })
                : JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
            return Task.CompletedTask;
        };
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
            Assert.That(harness.Backoff.Attempts, Is.EqualTo(new[] { 1 }));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_failsWhenTheAgentHoldsThePipeOpen()
    {
        var harness = await HarnessAsync();
        harness.Process.HoldPrompt = true;
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(
            Request(harness.Workspace.Id) with { PromptTimeoutMs = 50 },
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Failed));
            Assert.That(result.StopReason, Is.EqualTo("unresponsive"));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_completesWhenARealAgentStopsUsingToolsAfterWorking()
    {
        var harness = await HarnessAsync();
        var turn = 0;
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
        harness.Process.BeforePromptCompletes = _ => ++turn == 1
            ? harness.Process.SendNotificationAsync("session/update", Update(new { sessionUpdate = "tool_call", toolCallId = "edit-1" }))
            : harness.Process.SendNotificationAsync("session/update", Update(new { sessionUpdate = "agent_message_chunk", content = new { type = "text", text = "Done: the special is live." } }));
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        // No vendor ACP agent reports "completed"; stopping after the work is how it hands back.
        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
            Assert.That(result.StopReason, Is.EqualTo("settled"));
            Assert.That(result.Iterations, Is.EqualTo(2));
            Assert.That(result.Reply, Is.EqualTo("Done: the special is live."));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_keepsWaitingWhileTheAgentKeepsWritingPastTheSilenceWindow()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true });
        harness.Process.BeforePromptCompletes = async cancellationToken =>
        {
            for (var beat = 0; beat < 12; beat++)
            {
                await harness.Process.SendNotificationAsync("session/update", Update(new { sessionUpdate = "agent_thought_chunk", content = new { type = "text", text = "." } }));
                await Task.Delay(20, cancellationToken);
            }
        };
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(
            Request(harness.Workspace.Id) with { PromptTimeoutMs = 100 },
            CancellationToken.None);

        // The turn lasts about 240ms against a 100ms window, but it never goes quiet for 100ms.
        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_stopsATurnThatKeepsRepeatingOneFailingToolCall()
    {
        var harness = await HarnessAsync();
        harness.Process.HoldPrompt = true;
        harness.Process.BeforePromptCompletes = async _ =>
        {
            for (var attempt = 0; attempt < 5; attempt++)
                await harness.Process.SendNotificationAsync("session/update", Update(new
                {
                    sessionUpdate = "tool_call_update",
                    toolCallId = $"fetch-{attempt}",
                    title = "webfetch",
                    status = "failed",
                    rawOutput = new { error = "invalid arguments" }
                }));
        };
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var result = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        // Each failure is an event, so the silence window alone would never end this turn.
        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Failed));
            Assert.That(result.StopReason, Is.EqualTo("stuck"));
            Assert.That(result.BlockReason, Does.Contain("5 times"));
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task StartFreshSessionAsync_opensANewAcpSessionOnTheRunningProcess()
    {
        var harness = await HarnessAsync();
        var missing = await harness.Scheduling.StartFreshSessionAsync(harness.Workspace.Id, "#1", CancellationToken.None);
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var fresh = await harness.Scheduling.StartFreshSessionAsync(harness.Workspace.Id, "#1 Say Ahoy", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(missing.Found, Is.False);
            Assert.That(fresh.Error, Is.Null);
            Assert.That(harness.Process.Methods.Count(method => method == "session/new"), Is.EqualTo(2));
            Assert.That(harness.Process.Methods.Count(method => method == "initialize"), Is.EqualTo(1), "the agent process is reused");
        });
        await harness.Scheduling.DisposeAsync();
    }

    [Test]
    public async Task RunAsync_completesAReviewThatRepliesWithoutATool()
    {
        var harness = await HarnessAsync();
        harness.Process.PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn" });
        harness.Process.BeforePromptCompletes = _ => harness.Process.SendNotificationAsync("session/update",
            Update(new { sessionUpdate = "agent_message_chunk", content = new { type = "text", text = "Correct.\nVERDICT: APPROVE" } }));
        await harness.Scheduling.ScheduleAsync(harness.Workspace.Id, "codex", CancellationToken.None);

        var review = await harness.Loop.RunAsync(Request(harness.Workspace.Id) with { ReplyCompletes = true }, CancellationToken.None);
        var author = await harness.Loop.RunAsync(Request(harness.Workspace.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That((review.Terminal, review.StopReason), Is.EqualTo((LoopTerminal.Completed, "replied")));
            Assert.That(review.Reply, Does.EndWith("VERDICT: APPROVE"));
            Assert.That(author.Terminal, Is.Not.EqualTo(LoopTerminal.Completed), "an author has to change something");
        });
        await harness.Scheduling.DisposeAsync();
    }

    private static JsonElement Update(object update) =>
        JsonSerializer.SerializeToElement(new { sessionId = "session-1", update });

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
        var backoff = new ImmediateLoopBackoff();
        return new Harness(workspace, process, scheduling, new AutonomousLoopService(scheduling, new LoopInvariantGuard(), backoff), backoff);
    }

    private sealed record Harness(
        Workspace Workspace,
        FakeAgentProcessProvider Process,
        AgentSchedulingService Scheduling,
        AutonomousLoopService Loop,
        ImmediateLoopBackoff Backoff);
}
