using System.Threading.Channels;
using AgentUp.Server.Features.Audit.Models;

namespace AgentUp.Server.Features.Audit.Services;

public sealed class AuditEventBus
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Channel<AuditEvent>, Func<AuditEvent, bool>?> _subscribers = [];

    public AuditEventSubscription Subscribe(Func<AuditEvent, bool>? filter = null)
    {
        var channel = Channel.CreateBounded<AuditEvent>(
            new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_lock) _subscribers[channel] = filter;
        return new AuditEventSubscription(channel.Reader, () =>
        {
            lock (_lock) _subscribers.Remove(channel);
            channel.Writer.TryComplete();
        });
    }

    public void Publish(AuditEvent evt)
    {
        lock (_lock)
        {
            foreach (var entry in _subscribers.Where(pair => pair.Value?.Invoke(evt) ?? true))
                entry.Key.Writer.TryWrite(evt);
        }
    }
}

public sealed class AuditEventSubscription(ChannelReader<AuditEvent> reader, Action dispose) : IAsyncDisposable
{
    public ChannelReader<AuditEvent> Reader => reader;

    public ValueTask DisposeAsync()
    {
        dispose();
        return ValueTask.CompletedTask;
    }
}
