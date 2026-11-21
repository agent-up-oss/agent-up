using System.Text.Json;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Capabilities.Providers;

/// <summary>
/// Capability settings under the Server data directory. The file can hold a token, so it is
/// written readable by the Server user only, the way agent and forge credentials are.
/// </summary>
public sealed class CapabilitySettingsStore(string dataDirectory) : ICapabilitySettingsStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly object _gate = new();
    private readonly string _path = Path.Join(dataDirectory, "capabilities", "settings.json");

    public IReadOnlyDictionary<string, string> Read(string capabilityId)
    {
        lock (_gate)
        {
            return ReadAll().TryGetValue(capabilityId, out var values)
                ? new Dictionary<string, string>(values, StringComparer.Ordinal)
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public void Write(string capabilityId, IReadOnlyDictionary<string, string> values)
    {
        lock (_gate)
        {
            var all = ReadAll();
            all[capabilityId] = new Dictionary<string, string>(values, StringComparer.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(all, Json));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            File.Move(temporary, _path, overwrite: true);
        }
    }

    private Dictionary<string, Dictionary<string, string>> ReadAll()
    {
        if (!File.Exists(_path))
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var read = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(_path), Json);
            return read is null
                ? new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, Dictionary<string, string>>(read, StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
