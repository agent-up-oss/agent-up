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
public sealed class AgentsLoopSurfaceTests
{
    [Test]
    public async Task PromptWaitAsync_returnsAfterTheScheduledPromptCompletes()
    {
        var host = await HostAsync();
        try
        {
            var result = await host.Controller.PromptWaitAsync(host.WorkspaceId, "hello", CancellationToken.None);

            Assert.That(result.Error, Is.Null);
        }
        finally
        {
            await host.Scheduling.DisposeAsync();
        }
    }

    [Test]
    public async Task AttachPolicyAndInspectLoop_surfaceTheLiveSession()
    {
        var host = await HostAsync();
        try
        {
            var attached = host.Controller.AttachPolicy(
                host.WorkspaceId, new AllowListedPermissionPolicy("allow-once", "allow-all"));
            var snapshot = host.Controller.InspectLoop(host.WorkspaceId);

            Assert.Multiple(() =>
            {
                Assert.That(attached, Is.True);
                Assert.That(snapshot.PromptGateReleased, Is.True);
                Assert.That(snapshot.PermissionsDrained, Is.True);
            });
        }
        finally
        {
            await host.Scheduling.DisposeAsync();
        }
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
        return new Host(new AgentsController(scheduling, events), scheduling, workspace.Id);
    }

    private sealed record Host(AgentsController Controller, AgentSchedulingService Scheduling, string WorkspaceId);
}
