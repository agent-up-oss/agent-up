using System.Collections.Concurrent;
using AgentUp.Server.Features.Processes.Repositories;

namespace AgentUp.Server.Tests.Fake;

internal sealed class InMemoryOutputRepository : IOutputRepository
{
    private readonly ConcurrentDictionary<(string, string), List<string>> _lines = new();

    public Task AppendAsync(string workspaceId, string appName, string line, CancellationToken ct = default)
    {
        _lines.GetOrAdd((workspaceId, appName), _ => []).Add(line);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        var lines = _lines.TryGetValue((workspaceId, appName), out var l) ? l : [];
        return Task.FromResult<IReadOnlyList<string>>(lines);
    }

    public Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        _lines.TryRemove((workspaceId, appName), out _);
        return Task.CompletedTask;
    }
}
