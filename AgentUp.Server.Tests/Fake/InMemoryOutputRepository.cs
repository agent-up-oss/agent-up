using System.Collections.Concurrent;
using AgentUp.Server.Features.Processes.Interfaces;

namespace AgentUp.Server.Tests.Fake;

/// <summary>
/// Collects process output in memory instead of on disk.
/// </summary>
/// <remarks>
/// Appends and reads are serialised per stream. A process pump appends from its own thread
/// while a test polls for a line, and the list this stands in for - a file the real
/// repository appends to a line at a time - tolerates that. Returning the live list did
/// not: a caller's ToList() reads Count, allocates, then copies, so an append in between
/// threw "Destination array was not long enough". Concurrent Add calls could also lose a
/// line outright, because ConcurrentDictionary makes the dictionary safe, not the lists
/// inside it.
/// </remarks>
internal sealed class InMemoryOutputRepository : IOutputRepository
{
    private readonly ConcurrentDictionary<(string, string), List<string>> _lines = new();

    public Task AppendAsync(string workspaceId, string appName, string line, CancellationToken ct = default)
    {
        var stream = _lines.GetOrAdd((workspaceId, appName), _ => []);
        lock (stream)
            stream.Add(line);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        if (!_lines.TryGetValue((workspaceId, appName), out var stream))
            return Task.FromResult<IReadOnlyList<string>>([]);

        lock (stream)
            return Task.FromResult<IReadOnlyList<string>>(stream.ToArray());
    }

    public Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        _lines.TryRemove((workspaceId, appName), out _);
        return Task.CompletedTask;
    }
}
