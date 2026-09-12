using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Git.Providers;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Features.Agents.Unit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Git.Provider;

[TestFixture]
public sealed class WorkspaceAgentPromptGuardTests
{
    private AgentSchedulingService _scheduling = null!;
    private FakeAgentProcessProvider _process = null!;
    private Workspace _workspace = null!;
    private WorkspaceAgentPromptGuard _guard = null!;

    [SetUp]
    public async Task SetUp()
    {
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService([])),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        _workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest("Workspace", "/repo", "/repo", "main", "abc"));
        _process = new FakeAgentProcessProvider();
        var command = OperatingSystem.IsWindows()
            ? Path.Join(Environment.SystemDirectory, "cmd.exe")
            : "/bin/sh";
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Agents:Codex:Command"] = command }).Build(), []);
        var events = new AgentEventService(new AgentEventFrameProvider());
        _scheduling = new AgentSchedulingService(
            new WorkspaceQueryController(registry),
            new FakeAgentProcessFactory(_process),
            commands,
            new AgentEventFrameProvider(),
            events,
            NullLogger<AgentSchedulingService>.Instance);
        _guard = new WorkspaceAgentPromptGuard(new AgentsController(_scheduling, events));
    }

    [TearDown]
    public async Task TearDown() => await _scheduling.DisposeAsync();

    [Test]
    public void IsPromptRunning_isFalseWhenNoAgentIsScheduled()
    {
        Assert.That(_guard.IsPromptRunning(_workspace.Id), Is.False);
    }

    [Test]
    public async Task IsPromptRunning_isTrueOnlyWhileAPromptIsInFlight()
    {
        await _scheduling.ScheduleAsync(_workspace.Id, AgentKind.Codex, CancellationToken.None);
        _process.HoldPrompt = true;
        await _scheduling.PromptAsync(_workspace.Id, "hello", CancellationToken.None);

        Assert.That(_guard.IsPromptRunning(_workspace.Id), Is.True);

        _process.CompletePrompt();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (_scheduling.Get(_workspace.Id)?.State == "running")
            await Task.Delay(10, timeout.Token);

        Assert.That(_guard.IsPromptRunning(_workspace.Id), Is.False);
    }
}
