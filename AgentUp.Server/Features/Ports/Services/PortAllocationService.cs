using System.Net;
using System.Net.Sockets;
using System.Text.Json;
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
    private readonly string _storagePath;
    private readonly ILogger<PortAllocationService> _logger;

    public PortAllocationService(string storagePath, ILogger<PortAllocationService> logger)
    {
        _storagePath = storagePath;
        _logger = logger;
        LoadFromDisk();
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

                if (portCount == 0 || ArePortsAvailable(basePort, portCount))
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

    private static bool ArePortsAvailable(int basePort, int count)
    {
        for (var i = 0; i < count; i++)
        {
            if (!IsPortAvailable(basePort + i))
                return false;
        }
        return true;
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private void LoadFromDisk()
    {
        if (!File.Exists(_storagePath))
            return;

        try
        {
            var json = File.ReadAllText(_storagePath);
            var data = JsonSerializer.Deserialize<PortRangeData>(json);
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
            _logger.LogWarning(ex, "Failed to load port range assignments from {Path}", _storagePath);
        }
    }

    private async Task SaveToDiskAsync()
    {
        var data = new PortRangeData(
            _highWaterMark,
            new Dictionary<string, int>(_workspaceRanges),
            [.. _freeRanges]);

        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

        var dir = Path.GetDirectoryName(_storagePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        var tmp = _storagePath + ".tmp";
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, _storagePath, overwrite: true);
    }
}

internal sealed record PortRangeData(
    int HighWaterMark,
    Dictionary<string, int> Ranges,
    List<int>? FreeRanges = null);
