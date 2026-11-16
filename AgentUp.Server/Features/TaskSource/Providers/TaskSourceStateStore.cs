using System.Collections.Concurrent;
using AgentUp.Server.Features.TaskSource.Interfaces;

namespace AgentUp.Server.Features.TaskSource.Providers;

/// <summary>
/// The one-time values that tie an authorize redirect to the request that started it.
///
/// A state is spent on first use: a callback replayed with the same state finds nothing and is
/// refused, which is the point of carrying one.
/// </summary>
public sealed class TaskSourceStateStore : ITaskSourceStateStore
{
    private readonly ConcurrentDictionary<string, string> _states = new(StringComparer.Ordinal);

    public string Issue(string moduleId)
    {
        var state = Guid.NewGuid().ToString("N");
        _states[state] = moduleId;
        return state;
    }

    public string? Consume(string state)
        => _states.TryRemove(state, out var moduleId) ? moduleId : null;
}
