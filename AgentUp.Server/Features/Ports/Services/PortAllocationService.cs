using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Ports.Models;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Ports.Services;

public sealed class PortAllocationService : IPortAllocationService
{
    private const int BasePort = 10000;
    private const int RangeSize = 100;
    private const int MaxConflictRetries = 20;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, int> _workspaceRanges = new();
    private readonly Queue<int> _freeRanges = new();
    private int _highWaterMark = 0;
    private readonly IPortRangeStore _store;
    private readonly IPortAvailabilityProvider _ports;
    private readonly ILogger<PortAllocationService> _logger;

    public PortAllocationService(
        IPortRangeStore store,
        IPortAvailabilityProvider ports,
        ILogger<PortAllocationService> logger)
    {
        _store = store;
        _ports = ports;
        _logger = logger;
        Load();
    }

    public async Task<int> GetBasePortAsync(string workspaceId)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_workspaceRanges.TryGetValue(workspaceId, out var rangeIndex))
            {
                rangeIndex = NextRangeIndex();
                _workspaceRanges[workspaceId] = rangeIndex;
                await SaveToDiskAsync();
                _logger.LogInformation(
                    "Assigned port range {Base}-{End} to workspace {Id}",
                    BasePort + rangeIndex * RangeSize,
                    BasePort + rangeIndex * RangeSize + RangeSize - 1,
                    workspaceId);
            }

            return BasePort + rangeIndex * RangeSize;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<int> GetConflictFreeBasePortAsync(string workspaceId, int portCount)
    {
        await _lock.WaitAsync();
        try
        {
            for (var attempt = 0; attempt < MaxConflictRetries; attempt++)
            {
                if (!_workspaceRanges.TryGetValue(workspaceId, out var rangeIndex))
                {
                    rangeIndex = NextRangeIndex();
                    _workspaceRanges[workspaceId] = rangeIndex;
                }

                var basePort = BasePort + rangeIndex * RangeSize;

                if (portCount == 0 || _ports.ArePortsAvailable(basePort, portCount))
                {
                    await SaveToDiskAsync();
                    if (attempt > 0)
                        _logger.LogInformation(
                            "Settled on port range {Base}-{End} for workspace {Id} after {Attempts} attempt(s)",
                            basePort, basePort + portCount - 1, workspaceId, attempt + 1);
                    return basePort;
                }

                // Port conflict — drop this range and try a completely fresh one.
                // Conflicted ranges are not returned to the free pool because something
                // external is using them; they simply become untracked.
                _logger.LogWarning(
                    "Port conflict in range {Base}-{End} for workspace {Id} (attempt {Attempt}), assigning new range",
                    basePort, basePort + portCount - 1, workspaceId, attempt + 1);

                _workspaceRanges.Remove(workspaceId);
                var freshIndex = _highWaterMark++;
                _workspaceRanges[workspaceId] = freshIndex;
            }

            throw new InvalidOperationException(
                $"Could not find a conflict-free port range for workspace {workspaceId} after {MaxConflictRetries} attempts.");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ReleaseAsync(string workspaceId)
    {
        await _lock.WaitAsync();
        try
        {
            if (_workspaceRanges.Remove(workspaceId, out var rangeIndex))
            {
                _freeRanges.Enqueue(rangeIndex);
                await SaveToDiskAsync();
                _logger.LogInformation(
                    "Released port range {Base}-{End} (index {Index}) for workspace {Id} — returned to free pool",
                    BasePort + rangeIndex * RangeSize,
                    BasePort + rangeIndex * RangeSize + RangeSize - 1,
                    rangeIndex,
                    workspaceId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    // Prefer recycled ranges from deleted workspaces; fall back to a fresh index.
    private int NextRangeIndex() =>
        _freeRanges.Count > 0 ? _freeRanges.Dequeue() : _highWaterMark++;

    private void Load()
    {
        try
        {
            var data = _store.Load();
            if (data is null)
                return;

            foreach (var (id, idx) in data.Ranges)
                _workspaceRanges[id] = idx;

            if (data.FreeRanges is not null)
                foreach (var idx in data.FreeRanges)
                    _freeRanges.Enqueue(idx);

            _highWaterMark = data.HighWaterMark;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load port range assignments");
        }
    }

    private async Task SaveToDiskAsync()
    {
        var data = new PortRangeData(
            _highWaterMark,
            new Dictionary<string, int>(_workspaceRanges),
            [.. _freeRanges]);

        await _store.SaveAsync(data);
    }
}
