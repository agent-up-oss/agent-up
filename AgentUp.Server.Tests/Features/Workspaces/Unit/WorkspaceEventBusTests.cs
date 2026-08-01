using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceEventBusTests
{
    [Test]
    public async Task Publish_DeliversEventToSubscriber()
    {
        var bus = new WorkspaceEventBus();
        await using var sub = bus.Subscribe();
        var evt = Event("ws-1", "Running");

        bus.Publish(evt);

        var received = await sub.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(received, Is.EqualTo(evt));
    }

    [Test]
    public async Task Publish_DeliversEventToAllSubscribers()
    {
        var bus = new WorkspaceEventBus();
        await using var first = bus.Subscribe();
        await using var second = bus.Subscribe();
        var evt = Event("ws-1", "Running");

        bus.Publish(evt);

        Assert.That(await first.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(evt));
        Assert.That(await second.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1)), Is.EqualTo(evt));
    }

    [Test]
    public async Task DisposedSubscription_StopsReceivingEvents()
    {
        var bus = new WorkspaceEventBus();
        var sub = bus.Subscribe();

        await sub.DisposeAsync();
        bus.Publish(Event("ws-1", "Running"));

        var canRead = await sub.Reader.WaitToReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(1));
        Assert.That(canRead, Is.False);
    }

    [Test]
    public async Task Publish_DropsOldestEvent_WhenSubscriberQueueOverflows()
    {
        var bus = new WorkspaceEventBus();
        await using var sub = bus.Subscribe();

        for (var i = 0; i < 60; i++)
            bus.Publish(Event($"ws-{i}", "Running"));

        var received = new List<WorkspaceStateChangedEvent>();
        while (sub.Reader.TryRead(out var evt))
            received.Add(evt);

        Assert.Multiple(() =>
        {
            Assert.That(received, Has.Count.EqualTo(50));
            Assert.That(received.Select(evt => evt.WorkspaceId), Does.Not.Contain("ws-0"));
            Assert.That(received.Last().WorkspaceId, Is.EqualTo("ws-59"));
        });
    }

    private static WorkspaceStateChangedEvent Event(string workspaceId, string state) =>
        new(workspaceId, state, [new AppStateChange("App", state)]);
}
