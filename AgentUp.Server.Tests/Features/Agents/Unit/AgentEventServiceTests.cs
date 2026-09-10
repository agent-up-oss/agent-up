using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentEventServiceTests
{
    [Test]
    public async Task SubscribeAsync_replaysOnlyEventsAfterCursor()
    {
        var service = new AgentEventService(new AgentEventFrameProvider());
        var first = service.Publish("ws", "one", new { value = 1 });
        service.Publish("ws", "two", new { value = 2 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await using var events = service.SubscribeAsync("ws", first.Sequence, timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.That(await events.MoveNextAsync(), Is.True);
        Assert.Multiple(() => { Assert.That(events.Current.Type, Is.EqualTo("two")); Assert.That(events.Current.Sequence, Is.GreaterThan(first.Sequence)); });
    }
}
