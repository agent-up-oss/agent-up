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

    [Test]
    public async Task Remove_completesSubscribersAndDropsReplayHistory()
    {
        var service = new AgentEventService(new AgentEventFrameProvider());
        service.Publish("ws", "one", new { value = 1 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await using var events = service.SubscribeAsync("ws", 0, timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.That(await events.MoveNextAsync(), Is.True);

        service.Remove("ws");
        service.Publish("ws", "two", new { value = 2 });

        await using var after = service.SubscribeAsync("ws", 0, timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.That(await after.MoveNextAsync(), Is.True);
        Assert.That(after.Current.Type, Is.EqualTo("two"));
    }
}
