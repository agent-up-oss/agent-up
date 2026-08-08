using System.Threading.Channels;
using AgentUp.Server.Features.Browser.Models;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserEventBus
{
    private readonly Lock _lock = new();
    private readonly List<Channel<string>> _subscribers = [];
    private readonly Dictionary<string, string> _latestStreamStates = [];

    public BrowserEventSubscription Subscribe()
    {
        var ch = Channel.CreateBounded<string>(
            new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_lock)
        {
            _subscribers.Add(ch);
            foreach (var ev in _latestStreamStates.Values)
                ch.Writer.TryWrite(ev);
        }
        return new BrowserEventSubscription(ch.Reader, () =>
        {
            lock (_lock) _subscribers.Remove(ch);
            ch.Writer.TryComplete();
        });
    }

    public async Task StreamToResponseAsync(HttpResponse response, CancellationToken ct)
    {
        await using var sub = Subscribe();
        try
        {
            await foreach (var json in sub.Reader.ReadAllAsync(ct))
            {
                await response.WriteAsync($"data: {json}\n\n", ct);
                await response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return;
        }
    }

    public void PublishStreamState(string workspaceId, StreamState state)
    {
        var json = StreamStateEvent.From(workspaceId, state).Serialize();
        lock (_lock)
        {
            _latestStreamStates[workspaceId] = json;
            foreach (var sub in _subscribers)
                sub.Writer.TryWrite(json);
        }
    }

    // Called on workspace remove so a re-subscribing client doesn't replay a stale entry.
    public void RemoveWorkspaceStreamStateCache(string workspaceId)
    {
        lock (_lock)
        {
            _latestStreamStates.Remove(workspaceId);
        }
    }
}

public sealed class BrowserEventSubscription(ChannelReader<string> reader, Action dispose)
    : IAsyncDisposable
{
    public ChannelReader<string> Reader => reader;
    public ValueTask DisposeAsync() { dispose(); return ValueTask.CompletedTask; }
}
