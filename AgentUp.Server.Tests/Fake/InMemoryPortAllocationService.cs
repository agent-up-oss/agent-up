using AgentUp.Server.Features.Ports.Services;

namespace AgentUp.Server.Tests.Fake;

internal sealed class InMemoryPortAllocationService : IPortAllocationService
{
    private readonly Dictionary<string, int> _ranges = new();
    private int _next = 0;

    public Task<int> GetBasePortAsync(string workspaceId)
    {
        if (!_ranges.TryGetValue(workspaceId, out var idx))
        {
            idx = _next++;
            _ranges[workspaceId] = idx;
        }
        return Task.FromResult(10000 + idx * 100);
    }

    public Task ReleaseAsync(string workspaceId)
    {
        _ranges.Remove(workspaceId);
        return Task.CompletedTask;
    }
}
