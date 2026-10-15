using System.Collections.Concurrent;
using AgentUp.Server.Features.Forge.Interfaces;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class ForgeOAuthStateStore : IForgeOAuthStateStore
{
    private readonly ConcurrentDictionary<string, string> _states = new(StringComparer.Ordinal);

    public string Issue(string workspaceId)
    {
        var state = Guid.NewGuid().ToString("N");
        _states[state] = workspaceId;
        return state;
    }

    public string? Consume(string state)
        => _states.TryRemove(state, out var workspaceId) ? workspaceId : null;
}
