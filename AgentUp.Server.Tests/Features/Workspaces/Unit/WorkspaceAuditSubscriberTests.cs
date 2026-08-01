using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceAuditSubscriberTests
{
    [Test]
    public async Task ExecuteAsync_SkipsConsecutiveDuplicateWorkspaceStateEvents()
    {
        var bus = new WorkspaceEventBus();
        var events = new InMemoryAuditEventRepository();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriber = new WorkspaceAuditSubscriber(
            bus,
            ServerTestComposition.CreateAuditController(events: events),
            () => ready.TrySetResult());

        await subscriber.StartAsync(CancellationToken.None);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(2));
            bus.Publish(Event("Starting"));
            bus.Publish(Event("Starting"));
            bus.Publish(Event("Running"));

            await WaitUntilAsync(() => events.Events.Count >= 2);
        }
        finally
        {
            await subscriber.StopAsync(CancellationToken.None);
        }

        Assert.Multiple(() =>
        {
            Assert.That(events.Events, Has.Count.EqualTo(2));
            Assert.That(events.Events.Select(evt => evt.Outcome), Is.EqualTo(new[] { "Starting", "Running" }));
            Assert.That(events.Events.All(evt => evt.Details["scope"] == "workspace"), Is.True);
        });
    }

    [Test]
    public async Task ExecuteAsync_DoesNotStopWhenAuditWriteFailsAndRetriesDuplicateLater()
    {
        var bus = new WorkspaceEventBus();
        var events = new FailsOnceAuditEventRepository();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriber = new WorkspaceAuditSubscriber(
            bus,
            new AuditController(new AuditService(
                events,
                new InMemoryAuditArtifactRepository(),
                new FakeAuditIdentityProvider(),
                new WorkspaceQueryController(ServerTestComposition.CreateRegistry()))),
            () => ready.TrySetResult());

        await subscriber.StartAsync(CancellationToken.None);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(2));
            bus.Publish(Event("Running"));
            await WaitUntilAsync(() => events.AppendAttempts == 1);
            bus.Publish(Event("Running"));

            await WaitUntilAsync(() => events.Events.Count == 1);
        }
        finally
        {
            await subscriber.StopAsync(CancellationToken.None);
        }

        Assert.Multiple(() =>
        {
            Assert.That(events.AppendAttempts, Is.EqualTo(2));
            Assert.That(events.Events.Single().Outcome, Is.EqualTo("Running"));
        });
    }

    private static WorkspaceStateChangedEvent Event(string state) =>
        new("workspace", state, [new AppStateChange("Docs", state)]);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!predicate())
            await Task.Delay(25, cts.Token);
    }

    private sealed class FailsOnceAuditEventRepository : IAuditEventRepository
    {
        private readonly List<AuditEvent> _events = [];
        private bool _failed;

        public int AppendAttempts { get; private set; }
        public IReadOnlyList<AuditEvent> Events => _events;

        public Task AppendAsync(AuditEvent evt, CancellationToken cancellationToken)
        {
            AppendAttempts++;
            if (!_failed)
            {
                _failed = true;
                throw new IOException("audit unavailable");
            }

            _events.Add(evt);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> QueryAsync(
            AuditEventQuery query,
            CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<AuditEvent>>(_events);

        public Task<AuditEvent?> GetAsync(string eventId, CancellationToken cancellationToken)
            => Task.FromResult(_events.FirstOrDefault(evt => evt.EventId == eventId));
    }
}
