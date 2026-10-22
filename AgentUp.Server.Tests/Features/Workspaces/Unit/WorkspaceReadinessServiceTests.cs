using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;
using AgentUp.Server.Features.DesktopApplications.Providers;
using AgentUp.Server.Features.DesktopApplications.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Interfaces;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;
using AgentUp.Browser.Streaming;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceReadinessServiceTests
{
    [Test]
    public async Task Watch_promotesAPortApplicationWhenTcpAccepts()
    {
        var (service, registry, workspace, probe) = await CreateAsync(
            TcpPort());
        probe.Tcp.Enqueue(true);

        await service.WatchAsync(workspace.Id);

        var app = registry.GetById(workspace.Id)!.Applications.Single();
        Assert.Multiple(() =>
        {
            Assert.That(app.State, Is.EqualTo(ApplicationState.Running));
            Assert.That(registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Running));
        });
    }

    [Test]
    public async Task Watch_promotesAHealthCheckedApplicationWhenThePathSucceeds()
    {
        var (service, registry, workspace, probe) = await CreateAsync(
            HealthPort());
        probe.Http.Enqueue(true);

        await service.WatchAsync(workspace.Id);

        Assert.That(registry.GetById(workspace.Id)!.Applications.Single().State, Is.EqualTo(ApplicationState.Running));
    }

    [Test]
    public async Task Watch_failsWithTheTimedOutCondition()
    {
        var clock = new SequenceReadinessClock();
        var (service, registry, workspace, _) = await CreateAsync(
            TcpPort(),
            clock,
            new ApplicationReadinessOptions(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3), TimeSpan.Zero));

        await service.WatchAsync(workspace.Id);
        await WaitForAsync(() => registry.GetById(workspace.Id)!.Applications.Single().State == ApplicationState.Failed);

        var app = registry.GetById(workspace.Id)!.Applications.Single();
        Assert.Multiple(() =>
        {
            Assert.That(app.State, Is.EqualTo(ApplicationState.Failed));
            Assert.That(app.LastError, Is.EqualTo("port 10301 never accepted a connection within 3s"));
            Assert.That(registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Failed));
        });
    }

    [Test]
    public async Task Watch_returnsWhileStartingWhenTheProbeHasNotSucceeded()
    {
        var (service, registry, workspace, _) = await CreateAsync(
            TcpPort(),
            new SequenceReadinessClock(),
            new ApplicationReadinessOptions(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(2), TimeSpan.Zero));

        await service.WatchAsync(workspace.Id);

        Assert.That(registry.GetById(workspace.Id)!.Applications.Single().State, Is.EqualTo(ApplicationState.Starting));
        Assert.That(registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Starting));
        service.Stop(workspace.Id);
    }

    [Test]
    public async Task Watch_promotesAProcessWithNoPortsAfterTheSettleWindow()
    {
        var clock = new SequenceReadinessClock();
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await RegisterStartingAsync(registry);
        var service = CreateService(
            registry,
            new ScriptedReadinessProbe(),
            clock,
            new ApplicationReadinessOptions(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(2)));

        await service.WatchAsync(workspace.Id);
        Assert.That(registry.GetById(workspace.Id)!.Applications.Single().State, Is.EqualTo(ApplicationState.Starting));

        await WaitForAsync(() => registry.GetById(workspace.Id)!.Applications.Single().State == ApplicationState.Running);
        Assert.That(registry.GetById(workspace.Id)!.State, Is.EqualTo(WorkspaceState.Running));
    }

    [Test]
    public async Task Watch_promotesAgainAfterStopClearsThePoller()
    {
        var (service, registry, workspace, probe) = await CreateAsync(
            TcpPort());
        await service.WatchAsync(workspace.Id);
        service.Stop(workspace.Id);

        var app = registry.GetById(workspace.Id)!.Applications.Single();
        app.State = ApplicationState.Starting;
        registry.GetById(workspace.Id)!.State = WorkspaceState.Starting;
        probe.Tcp.Enqueue(true);

        await service.WatchAsync(workspace.Id);

        Assert.That(registry.GetById(workspace.Id)!.Applications.Single().State, Is.EqualTo(ApplicationState.Running));
    }

    [Test]
    public async Task Watch_emitsTheWorkspaceTransitionOnTheEventBus()
    {
        var bus = new WorkspaceEventBus();
        var registry = ServerTestComposition.CreateRegistry(null, bus);
        var created = await RegisterStartingAsync(registry, TcpPort());
        var probe = new ScriptedReadinessProbe();
        probe.Tcp.Enqueue(true);
        await using var subscription = bus.Subscribe();
        var service = CreateService(registry, probe, new SequenceReadinessClock());

        await service.WatchAsync(created.Id);

        var states = new List<string>();
        while (states.Count < 4 && subscription.Reader.TryRead(out var evt))
            states.Add(evt.State);
        Assert.That(states, Does.Contain("Running"));
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(2);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);
        Assert.That(condition(), Is.True, "timed out waiting for a readiness transition");
    }

    private static async Task<(WorkspaceReadinessService Service, WorkspaceRegistry Registry, Workspace Workspace, ScriptedReadinessProbe Probe)> CreateAsync(
        PortMapping port,
        IApplicationReadinessClock? clock = null,
        ApplicationReadinessOptions? options = null)
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await RegisterStartingAsync(registry, port);
        var probe = new ScriptedReadinessProbe();
        return (CreateService(registry, probe, clock ?? new SequenceReadinessClock(), options), registry, workspace, probe);
    }

    private static async Task<Workspace> RegisterStartingAsync(WorkspaceRegistry registry, PortMapping? port = null)
    {
        var definition = new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand);
        if (port is not null)
            definition = definition.WithPort(port.DefaultPort);
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(definition.Build())
            .Build());
        if (port is not null)
            workspace.Applications.Single().AllocatedPorts = [port];
        workspace.Applications.Single().State = ApplicationState.Starting;
        workspace.State = WorkspaceState.Starting;
        return workspace;
    }

    private static WorkspaceReadinessService CreateService(
        WorkspaceRegistry registry,
        IApplicationReadinessProbe probe,
        IApplicationReadinessClock clock,
        ApplicationReadinessOptions? options = null)
    {
        var processes = ServerTestComposition.CreateProcessesController(new NullWorkspaceProcessManager());
        var desktop = new DesktopApplicationsController(new DesktopSessionService(
            new FakeDesktopDisplayProvider(),
            new BrowserRemoteDisplayService(NullLogger<BrowserRemoteDisplayService>.Instance),
            new DesktopInputMessageProvider(),
            new DesktopViewerTicketProvider(),
            new FakeHostedDesktopNativeLibraryProvider(),
            NullLogger<DesktopSessionService>.Instance));
        return new WorkspaceReadinessService(
            registry,
            processes,
            desktop,
            probe,
            clock,
            options ?? ApplicationReadinessOptions.Immediate,
            NullLogger<WorkspaceReadinessService>.Instance);
    }

    private static PortMapping TcpPort() => new("WEB_PORT", 3000, 10301);

    private static PortMapping HealthPort() => new("WEB_PORT", 3000, 10300, "http", "/health");

    private sealed class ScriptedReadinessProbe : IApplicationReadinessProbe
    {
        public Queue<bool> Tcp { get; } = new();
        public Queue<bool> Http { get; } = new();

        public Task<bool> TcpReadyAsync(int port, CancellationToken cancellationToken)
            => Task.FromResult(Dequeue(Tcp));

        public Task<bool> HttpReadyAsync(int port, string path, CancellationToken cancellationToken)
            => Task.FromResult(Dequeue(Http));

        private static bool Dequeue(Queue<bool> values)
            => values.Count > 0 && values.Dequeue();
    }

    private sealed class SequenceReadinessClock : IApplicationReadinessClock
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        {
            UtcNow += delay;
            return Task.CompletedTask;
        }
    }
}
