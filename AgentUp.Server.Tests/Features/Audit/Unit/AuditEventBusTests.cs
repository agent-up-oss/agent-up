using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Audit.Services;

namespace AgentUp.Server.Tests.Features.Audit.Unit;

[TestFixture]
public sealed class AuditEventBusTests
{
    [Test]
    public async Task Publish_DeliversMatchingEventsToFilteredSubscription()
    {
        var bus = new AuditEventBus();
        await using var subscription = bus.Subscribe(evt =>
            string.Equals(evt.Kind, "frontend", StringComparison.OrdinalIgnoreCase));

        bus.Publish(CreateEvent("ignored", "health"));
        bus.Publish(CreateEvent("accepted", "frontend"));

        var received = await subscription.Reader.ReadAsync();
        await subscription.DisposeAsync();

        Assert.That(received.EventId, Is.EqualTo("accepted"));
    }

    [Test]
    public async Task Publish_DeliversAllEventsWhenFilterIsNull()
    {
        var bus = new AuditEventBus();
        await using var subscription = bus.Subscribe();

        bus.Publish(CreateEvent("first", "frontend"));
        bus.Publish(CreateEvent("second", "health"));

        var first = await subscription.Reader.ReadAsync();
        var second = await subscription.Reader.ReadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(first.EventId, Is.EqualTo("first"));
            Assert.That(second.EventId, Is.EqualTo("second"));
        });
    }

    [Test]
    public async Task DisposeAsync_StopsDeliveringEvents()
    {
        var bus = new AuditEventBus();
        var subscription = bus.Subscribe();
        await subscription.DisposeAsync();

        bus.Publish(CreateEvent("after-dispose", "frontend"));

        Assert.That(subscription.Reader.TryRead(out _), Is.False);
    }

    private static AuditEvent CreateEvent(string eventId, string kind)
        => new(
            eventId,
            DateTimeOffset.Parse("2026-08-22T12:00:00Z"),
            kind,
            "web",
            "action",
            "success",
            "ws-1",
            null,
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, string> { ["application"] = "web" },
            []);

}
