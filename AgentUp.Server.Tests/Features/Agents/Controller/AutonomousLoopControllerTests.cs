using System.Text.Json;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
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

namespace AgentUp.Server.Tests.Features.Agents.Controller;

[TestFixture]
public sealed class AutonomousLoopControllerTests
{
    [Test]
    public async Task RunAsync_returnsTheTypedTerminalFromTheLoop()
    {
        var (controller, scheduling, workspaceId) = await HostAsync();
        try
        {
            var result = await controller.RunAsync(
                new LoopRunRequest(
                    workspaceId,
                    "finish",
                    1,
                    new AllowListedPermissionPolicy("allow-once", "allow-all"),
                    100_000),
                CancellationToken.None);

            Assert.That(result.Terminal, Is.EqualTo(LoopTerminal.Completed));
        }
        finally
        {
            await scheduling.DisposeAsync();
        }
    }

    [Test]
    public async Task Invariants_surfaceHardFailuresOnTheControllerBoundary()
    {
        var (controller, scheduling, _) = await HostAsync();
        try
        {
            var violations = controller.Invariants(new LoopRunResultBuilder().PermissionsHeld().Build());

            Assert.That(violations, Is.Not.Empty);
        }
        finally
        {
            await scheduling.DisposeAsync();
        }
    }

    private static async Task<(AutonomousLoopController Controller, AgentSchedulingService Scheduling, string WorkspaceId)> HostAsync()
    {
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService()),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(
            ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
        var process = new FakeAgentProcessProvider
        {
            PromptResult = JsonSerializer.SerializeToElement(new { stopReason = "end_turn", completed = true })
        };
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
        await scheduling.ScheduleAsync(workspace.Id, "codex", CancellationToken.None);
        var loop = new AutonomousLoopService(scheduling, new LoopInvariantGuard());
        return (new AutonomousLoopController(loop), scheduling, workspace.Id);
    }
}
