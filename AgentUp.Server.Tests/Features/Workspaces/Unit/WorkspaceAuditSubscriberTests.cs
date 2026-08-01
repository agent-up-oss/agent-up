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
        var subscriber = new WorkspaceAuditSubscriber(
            bus,
            ServerTestComposition.CreateAuditController(events: events));

        await subscriber.StartAsync(CancellationToken.None);
        try
        {
            await Task.Delay(100);
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

    private static WorkspaceStateChangedEvent Event(string state) =>
        new("workspace", state, [new AppStateChange("Docs", state)]);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!predicate())
            await Task.Delay(25, cts.Token);
    }
}
