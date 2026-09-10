using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.Agents.Services;

public sealed class AgentEventService(AgentEventFrameProvider frames)
{
    private readonly ConcurrentDictionary<string, List<AgentEventDto>> _history = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<AgentEventDto>>> _subscribers = new();
    private long _sequence;

    public AgentEventDto Publish(string workspaceId, string type, object payload)
    {
        var item = new AgentEventDto(Interlocked.Increment(ref _sequence), type,
            frames.Payload(payload), DateTimeOffset.UtcNow);
        var history = _history.GetOrAdd(workspaceId, _ => []);
        lock (history) { history.Add(item); if (history.Count > 1000) history.RemoveRange(0, history.Count - 1000); }
        if (_subscribers.TryGetValue(workspaceId, out var subscribers))
            foreach (var channel in subscribers.Values) channel.Writer.TryWrite(item);
        return item;
    }

    public async IAsyncEnumerable<AgentEventDto> SubscribeAsync(
        string workspaceId, long after, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<AgentEventDto>(new BoundedChannelOptions(256) {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = false
        });
        var id = Guid.NewGuid();
        var subscribers = _subscribers.GetOrAdd(workspaceId, _ => new());
        subscribers[id] = channel;
        try {
            if (_history.TryGetValue(workspaceId, out var history))
            {
                AgentEventDto[] replay;
                lock (history) replay = history.Where(item => item.Sequence > after).ToArray();
                foreach (var item in replay) yield return item;
            }
            await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken)) yield return item;
        } finally { subscribers.TryRemove(id, out _); }
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
