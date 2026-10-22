using System.Collections.Concurrent;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Processes.Models;

namespace AgentUp.CLI.Tests.Fake;

internal sealed class InMemoryOutputRepository : IOutputRepository
{
    private readonly ConcurrentDictionary<(string, string), ProcessOutputBuffer> _buffers = new();

    public Task AppendAsync(string workspaceId, string appName, ProcessOutputLine line, CancellationToken ct = default)
    {
        _buffers.GetOrAdd((workspaceId, appName), _ => new ProcessOutputBuffer()).Append(line);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProcessOutputLine>> GetAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        var lines = _buffers.TryGetValue((workspaceId, appName), out var buffer)
            ? buffer.Snapshot()
            : [];
        return Task.FromResult(lines);
    }

    public Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        _buffers.TryRemove((workspaceId, appName), out _);
        return Task.CompletedTask;
    }
}
