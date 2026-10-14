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
using AgentUp.Server.Tests.Features.Agents.Unit;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class ScriptedLoopProviderTests
{
    [Test]
    public async Task PolicyGrant_recordsTheRuleAndCompletes()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.Permission);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
            Assert.That(result.PermissionRuleIds, Is.EqualTo(new[] { "allow-all" }));
        });
    }

    [Test]
    public async Task UnmatchedPolicy_stopsAsBlockedPolicy()
    {
        var result = await RunAsync(
            AllowAll() with { Policy = new AllowListedPermissionPolicy("missing", "none") },
            ScriptedTurnKind.Permission);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.BlockedPolicy));
    }

    [Test]
    public async Task ToolError_offersRecoveryThenCompletesTheNextTurn()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.ToolError, ScriptedTurnKind.Complete);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
            Assert.That(result.Process.RecoveryOffers, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NoProgressTurn_stopsAsBlockedAwaitingInput()
    {
        var result = await RunAsync(AllowAll() with { StandingPolicyReplies = 1 }, ScriptedTurnKind.EndTurn, ScriptedTurnKind.EndTurn);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.BlockedAwaitingInput));
    }

    [Test]
    public async Task RateLimited_backsOffThenBlocksRatherThanFailing()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.RateLimited, ScriptedTurnKind.RateLimited);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.BlockedRateLimited));
            Assert.That(result.Backoff.Attempts, Is.EqualTo(new[] { 1 }));
        });
    }

    [Test]
    public async Task RateLimited_completesAfterBackoff()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.RateLimited, ScriptedTurnKind.Complete);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
    }

    [Test]
    public async Task Complete_stopsAsCompleted()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.Complete);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
    }

    [Test]
    public async Task Crash_stopsAsFailed()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.Crash);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Failed));
    }

    [Test]
    public async Task HoldPipe_stopsAsFailedUnresponsive()
    {
        var result = await RunAsync(AllowAll() with { PromptTimeoutMs = 40 }, ScriptedTurnKind.HoldPipe);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Failed));
            Assert.That(result.StopReason, Is.EqualTo("unresponsive"));
        });
    }

    [Test]
    public async Task TruncatedJson_stopsAsATypedFailure()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.TruncatedJson);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Failed));
    }

    [Test]
    public async Task EmptyAndWhitespaceTurns_stopAsBlockedAwaitingInput()
    {
        var result = await RunAsync(AllowAll() with { StandingPolicyReplies = 1 }, ScriptedTurnKind.Empty, ScriptedTurnKind.Whitespace);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.BlockedAwaitingInput));
    }

    [Test]
    public async Task UnknownToolAndHugeArgs_doNotHangTheLoop()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.UnknownTool, ScriptedTurnKind.HugeArgs, ScriptedTurnKind.Complete);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
    }

    [Test]
    public async Task InvalidUtf8AndDuplicatePermission_keepATypedTerminal()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.InvalidUtf8, ScriptedTurnKind.DuplicatePermission);

        Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
    }

    [Test]
    public async Task OutOfOrderPermission_doesNotLeaveAHeldLease()
    {
        var result = await RunAsync(AllowAll(), ScriptedTurnKind.OutOfOrder, ScriptedTurnKind.Complete);

        Assert.Multiple(() =>
        {
            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
            Assert.That(result.PermissionsDrained, Is.True);
        });
    }

    private static LoopRunRequest AllowAll() => new(
        "unused",
        "finish the task",
        6,
        new AllowListedPermissionPolicy("allow-once", "allow-all"),
        100_000);

    private static async Task<ScriptedRun> RunAsync(LoopRunRequest template, params ScriptedTurnKind[] turns)
    {
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService()),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(
            ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
        var process = new ScriptedAcpProcess(turns);
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var scheduling = new AgentSchedulingService(
            new WorkspaceQueryController(registry),
            new FakeAgentProcessFactory(process),
            new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?> { ["Agents:Codex:Command"] = command }).Build()),
            new FakeSubscriptionLoginProvider(),
            new FakeProcessEnvironmentProvider(),
            new FakeClaudeCredentialStore(),
            new AgentSubscriptionAuth(),
            new AgentEventFrameProvider(),
            new AgentEventService(new AgentEventFrameProvider()),
            NullLogger<AgentSchedulingService>.Instance);
        await scheduling.ScheduleAsync(workspace.Id, "codex", CancellationToken.None);
        var backoff = new ImmediateLoopBackoff();
        var loop = new AutonomousLoopService(scheduling, new LoopInvariantGuard(), backoff);
        var result = await loop.RunAsync(template with { WorkspaceId = workspace.Id }, CancellationToken.None);
        await scheduling.DisposeAsync();
        return new ScriptedRun(result.Terminal, result.StopReason, result.PermissionRuleIds, result.PermissionsDrained, process, backoff);
    }

    private sealed record ScriptedRun(
        LoopTerminal Terminal,
        string? StopReason,
        IReadOnlyList<string> PermissionRuleIds,
        bool PermissionsDrained,
        ScriptedAcpProcess Process,
        ImmediateLoopBackoff Backoff);
}
