using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Models;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.Agents.Services;

public sealed class AgentEventService(AgentEventFrameProvider frames)
{
    private readonly ConcurrentDictionary<string, AgentEventStreamState> _streams = new();
    private long _sequence;

    public AgentEventDto Publish(string workspaceId, string type, object payload)
    {
        var item = new AgentEventDto(Interlocked.Increment(ref _sequence), type,
            frames.Payload(payload), DateTimeOffset.UtcNow);
        var stream = _streams.GetOrAdd(workspaceId, _ => new AgentEventStreamState());
        lock (stream.SyncRoot)
        {
            stream.History.Add(item);
            if (stream.History.Count > 1000) stream.History.RemoveRange(0, stream.History.Count - 1000);
            foreach (var subscriber in stream.Subscribers.ToArray().Where(subscriber => !subscriber.Value.Writer.TryWrite(item)))
            {
                subscriber.Value.Writer.TryComplete();
                stream.Subscribers.Remove(subscriber.Key);
            }
        }
        return item;
    }

    public async IAsyncEnumerable<AgentEventDto> SubscribeAsync(
        string workspaceId, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<AgentEventDto>(new BoundedChannelOptions(256) {
            FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false
        });
        var id = Guid.NewGuid();
        var stream = _streams.GetOrAdd(workspaceId, _ => new AgentEventStreamState());
        AgentEventDto[] replay;
        lock (stream.SyncRoot)
        {
            replay = stream.History.Where(item => item.Sequence > after).ToArray();
            stream.Subscribers[id] = channel;
        }
        try {
            foreach (var item in replay) yield return item;
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken)) yield return item;
        } finally { lock (stream.SyncRoot) stream.Subscribers.Remove(id); }
    }

    public async Task WriteAsync(string workspaceId, long after, HttpResponse response, CancellationToken cancellationToken)
    {
        response.StatusCode = 200; response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache"; response.Headers.Append("X-Accel-Buffering", "no");
        try
        {
            await foreach (var item in SubscribeAsync(workspaceId, after, cancellationToken))
            {
                await response.WriteAsync(frames.Frame(item), cancellationToken);
                await response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return; }
    }
}
