using AgentUp.Server.Features.Processes.Interfaces;

namespace AgentUp.Server.Benchmarks.Fake;

public sealed class InMemoryOutputRepository : IOutputRepository
{
    public Task AppendAsync(string workspaceId, string appName, string line, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<string>> GetAsync(string workspaceId, string appName, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default)
        => Task.CompletedTask;
}
