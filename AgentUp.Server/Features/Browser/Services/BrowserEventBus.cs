using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserEventBus
{
    private readonly Lock _lock = new();
    private readonly List<Channel<string>> _subscribers = [];
    private string _latestChromiumEvent =
        """{"type":"chromium-status","state":"not_started","progress":0}""";
    private readonly Dictionary<string, string> _latestConnectivityEvents = [];

    public BrowserEventSubscription Subscribe()
    {
        var ch = Channel.CreateBounded<string>(
            new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_lock)
        {
            _subscribers.Add(ch);
            ch.Writer.TryWrite(_latestChromiumEvent);
            foreach (var ev in _latestConnectivityEvents.Values)
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

    public void PublishConnectivity(string workspaceId, string state, int attempt, int maxAttempts)
    {
        var json = JsonSerializer.Serialize(new { type = "browser-connectivity", workspaceId, state, attempt, maxAttempts });
        lock (_lock)
        {
            _latestConnectivityEvents[workspaceId] = json;
            foreach (var sub in _subscribers)
                sub.Writer.TryWrite(json);
        }
    }

    public void PublishChromiumStatus(string state, int progress)
    {
        var json = JsonSerializer.Serialize(new { type = "chromium-status", state, progress });
        lock (_lock)
        {
            _latestChromiumEvent = json;
            foreach (var sub in _subscribers)
                sub.Writer.TryWrite(json);
        }
    }
}

public sealed class BrowserEventSubscription(ChannelReader<string> reader, Action dispose)
    : IAsyncDisposable
{
    public ChannelReader<string> Reader => reader;
    public ValueTask DisposeAsync() { dispose(); return ValueTask.CompletedTask; }
}
