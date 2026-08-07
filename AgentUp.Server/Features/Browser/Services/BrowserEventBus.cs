using System.Text.Json;
using System.Threading.Channels;

namespace AgentUp.Server.Features.Browser.Services;

public sealed class BrowserEventBus
{
    private readonly Lock _lock = new();
    private readonly List<Channel<string>> _subscribers = [];
    private string _latestChromiumEvent =
        """{"type":"chromium-status","state":"not_started","progress":0}""";

    public BrowserEventSubscription Subscribe()
    {
        var ch = Channel.CreateBounded<string>(
            new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_lock)
        {
            _subscribers.Add(ch);
            // Replay the last known chromium state to the new subscriber immediately.
            ch.Writer.TryWrite(_latestChromiumEvent);
        }
        return new BrowserEventSubscription(ch.Reader, () =>
        {
            lock (_lock) _subscribers.Remove(ch);
            ch.Writer.TryComplete();
        });
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
