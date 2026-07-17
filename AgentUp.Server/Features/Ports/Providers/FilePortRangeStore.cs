using System.Text.Json;
using AgentUp.Server.Features.Ports.Interfaces;
using AgentUp.Server.Features.Ports.Models;

namespace AgentUp.Server.Features.Ports.Providers;

public sealed class FilePortRangeStore : IPortRangeStore
{
    private readonly string _storagePath;

    public FilePortRangeStore(string storagePath)
    {
        _storagePath = storagePath;
    }

    public PortRangeData? Load()
    {
        if (!File.Exists(_storagePath))
            return null;

        var json = File.ReadAllText(_storagePath);
        return JsonSerializer.Deserialize<PortRangeData>(json);
    }

    public async Task SaveAsync(PortRangeData data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

        var dir = Path.GetDirectoryName(_storagePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        var tmp = _storagePath + ".tmp";
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, _storagePath, overwrite: true);
    }
}
