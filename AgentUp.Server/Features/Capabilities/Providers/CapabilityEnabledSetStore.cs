using System.Text.Json;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Capabilities.Providers;

public sealed class CapabilityEnabledSetStore : ICapabilityEnabledSetStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string _path;

    public CapabilityEnabledSetStore(string dataDirectory)
    {
        var directory = Path.Join(dataDirectory, "capabilities");
        Directory.CreateDirectory(directory);
        _path = Path.Join(directory, "enabled.json");
        SeedFromEnvironment(directory);
    }

    public EnabledCapabilitySetDto Read()
    {
        if (!File.Exists(_path))
            return new EnabledCapabilitySetDto();

        return JsonSerializer.Deserialize<EnabledCapabilitySetDto>(File.ReadAllText(_path), Json)
               ?? new EnabledCapabilitySetDto();
    }

    public void Write(EnabledCapabilitySetDto set)
        => File.WriteAllText(_path, JsonSerializer.Serialize(set, Json));

    private void SeedFromEnvironment(string directory)
    {
        if (File.Exists(_path))
            return;

        var seed = Environment.GetEnvironmentVariable("AGENTUP_CAPABILITY_ENABLED_PATH");
        if (string.IsNullOrWhiteSpace(seed) || !File.Exists(seed))
            return;

        File.Copy(seed, _path, overwrite: false);
    }
}
