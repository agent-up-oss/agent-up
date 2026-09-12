using System.IO.Pipelines;
using System.Text;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

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
        Assert.That(await events.MoveNextAsync(), Is.False);

        service.Publish("ws", "two", new { value = 2 });

        await using var after = service.SubscribeAsync("ws", 0, timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.That(await after.MoveNextAsync(), Is.True);
        Assert.That(after.Current.Type, Is.EqualTo("two"));
    }

    [Test]
    public async Task SubscribeAsync_doesNotAttachToARemovedStream()
    {
        var service = new AgentEventService(new AgentEventFrameProvider());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var cycles = Enumerable.Range(0, 20).Select(async _ =>
        {
            service.Publish("ws", "seed", new { value = 1 });
            await using var events = service.SubscribeAsync("ws", 0, timeout.Token).GetAsyncEnumerator(timeout.Token);
            Assert.That(await events.MoveNextAsync(), Is.True);
            service.Remove("ws");
            Assert.That(await events.MoveNextAsync(), Is.False);
        });

        await Task.WhenAll(cycles);
    }

    [Test]
    public void Remove_isIgnoredWhenTheWorkspaceHasNoStream()
    {
        var service = new AgentEventService(new AgentEventFrameProvider());

        service.Remove("missing");
    }

    [Test]
    public async Task Publish_trimsHistoryToTheMostRecentThousandEvents()
    {
        var service = new AgentEventService(new AgentEventFrameProvider());
        for (var index = 0; index < 1001; index++)
            service.Publish("ws", "item", new { index });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

        await using var events = service.SubscribeAsync("ws", 0, timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.That(await events.MoveNextAsync(), Is.True);
        Assert.That(events.Current.Sequence, Is.EqualTo(2));
    }

    [Test]
    public async Task Publish_dropsASubscriberWhoseChannelIsFull()
    {
        var service = new AgentEventService(new AgentEventFrameProvider());
        service.Publish("ws", "seed", new { index = -1 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var events = service.SubscribeAsync("ws", 0, timeout.Token).GetAsyncEnumerator(timeout.Token);
        Assert.That(await events.MoveNextAsync(), Is.True);

        for (var index = 0; index < 300; index++)
            service.Publish("ws", "item", new { index });

        var count = 0;
        while (await events.MoveNextAsync())
            count++;

        Assert.That(count, Is.EqualTo(256));
    }

    [Test]
    public async Task WriteAsync_writesServerSentEventsUntilCancelled()
    {
        var service = new AgentEventService(new AgentEventFrameProvider());
        service.Publish("ws", "one", new { value = 1 });
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;
        context.Features.Set<IHttpResponseBodyFeature>(new MemoryResponseBodyFeature(body));
        using var timeout = new CancellationTokenSource();
        var write = service.WriteAsync("ws", 0, context.Response, timeout.Token);
        await Task.Delay(50);
        await timeout.CancelAsync();
        await write;

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
            Assert.That(context.Response.ContentType, Is.EqualTo("text/event-stream"));
            Assert.That(Encoding.UTF8.GetString(body.ToArray()), Does.Contain("event: one"));
        });
    }
}

internal sealed class MemoryResponseBodyFeature : IHttpResponseBodyFeature
{
    public MemoryResponseBodyFeature(Stream stream)
    {
        Stream = stream;
        Writer = PipeWriter.Create(stream);
    }

    public Stream Stream { get; }
    public PipeWriter Writer { get; }
    public bool BufferingDisabled { get; private set; }
    public void DisableBuffering() => BufferingDisabled = true;
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SendFileAsync(string path, long offset, long? count, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
    public Task CompleteAsync() => Task.CompletedTask;
}
