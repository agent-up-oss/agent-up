using System.Collections.Concurrent;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Models;

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
    private readonly ConcurrentDictionary<(string, string), ProcessOutputBuffer> _buffers = new();
    private readonly ConcurrentDictionary<(string, string), object> _gates = new();
    private readonly int _capacity;

    public InMemoryOutputRepository(int capacity = ProcessOutputBuffer.DefaultCapacity)
    {
        _capacity = capacity;
    }

    public Task AppendAsync(string workspaceId, string appName, string line, CancellationToken ct = default)
        => AppendAsync(workspaceId, appName, new ProcessOutputLine(line, ProcessOutputStream.Stdout), ct);

    public Task AppendAsync(string workspaceId, string appName, ProcessOutputLine line, CancellationToken ct = default)
    {
        var key = (workspaceId, appName);
        var gate = _gates.GetOrAdd(key, _ => new object());
        lock (gate)
        {
            var buffer = _buffers.GetOrAdd(key, _ => new ProcessOutputBuffer(_capacity));
            buffer.Append(line);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProcessOutputLine>> GetAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        var key = (workspaceId, appName);
        if (!_buffers.TryGetValue(key, out var buffer))
            return Task.FromResult<IReadOnlyList<ProcessOutputLine>>([]);

        var gate = _gates.GetOrAdd(key, _ => new object());
        lock (gate)
            return Task.FromResult(buffer.Snapshot());
    }

    public Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        _buffers.TryRemove((workspaceId, appName), out _);
        return Task.CompletedTask;
    }
}
