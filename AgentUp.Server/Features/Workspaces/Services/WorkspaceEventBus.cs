using System.Threading.Channels;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Services;

public sealed record WorkspaceStateChangedEvent(
    string WorkspaceId,
    string State,
    IReadOnlyList<AppStateChange> Applications);

public sealed record AppStateChange(string Name, string State);

public sealed class WorkspaceEventBus
{
    private readonly Lock _lock = new();
    private readonly List<Channel<WorkspaceStateChangedEvent>> _subscribers = [];

    public WorkspaceSubscription Subscribe()
    {
        var ch = Channel.CreateBounded<WorkspaceStateChangedEvent>(
            new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest });
        lock (_lock) _subscribers.Add(ch);
        return new WorkspaceSubscription(ch.Reader, () =>
        {
            lock (_lock) _subscribers.Remove(ch);
            ch.Writer.TryComplete();
        });
    }

    public void Publish(WorkspaceStateChangedEvent evt)
    {
        lock (_lock)
            foreach (var sub in _subscribers)
                sub.Writer.TryWrite(evt);
    }

    public void PublishWorkspaceChange(Workspace workspace)
    {
        Publish(new WorkspaceStateChangedEvent(
            workspace.Id,
            workspace.State.ToString(),
            workspace.Applications
                .Select(a => new AppStateChange(a.Name, a.State.ToString()))
                .ToList()));
    }
}

public sealed class WorkspaceSubscription(ChannelReader<WorkspaceStateChangedEvent> reader, Action dispose)
    : IAsyncDisposable
{
    public ChannelReader<WorkspaceStateChangedEvent> Reader => reader;
    public ValueTask DisposeAsync() { dispose(); return ValueTask.CompletedTask; }
}
