using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace AgentUp.Server.Features.Ports.Services;

public sealed class PortAllocationService : IPortAllocationService
{
    private const int BasePort = 10000;
    private const int RangeSize = 100;

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, int> _workspaceRanges = new();
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
                rangeIndex = _highWaterMark++;
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

    public async Task ReleaseAsync(string workspaceId)
    {
        await _lock.WaitAsync();
        try
        {
            if (_workspaceRanges.Remove(workspaceId))
            {
                await SaveToDiskAsync();
                _logger.LogInformation("Released port range for workspace {Id}", workspaceId);
            }
        }
        finally
        {
            _lock.Release();
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

            _highWaterMark = data.HighWaterMark;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load port range assignments from {Path}", _storagePath);
        }
    }

    private async Task SaveToDiskAsync()
    {
        var data = new PortRangeData(_highWaterMark, new Dictionary<string, int>(_workspaceRanges));
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

        var dir = Path.GetDirectoryName(_storagePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        var tmp = _storagePath + ".tmp";
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, _storagePath, overwrite: true);
    }
}

internal sealed record PortRangeData(int HighWaterMark, Dictionary<string, int> Ranges);
