using System.Text.Json;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>Loads reusable loop-coverage scenarios from JSON files in a directory.</summary>
public sealed class LoopScenarioCatalog
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<LoopScenario> Load(string directory)
    {
        if (!Directory.Exists(directory))
            throw new InvalidOperationException($"Loop-coverage scenarios were not found at '{directory}'.");

        return Directory.GetFiles(directory, "*.json")
            .Order(StringComparer.Ordinal)
            .Select(path => JsonSerializer.Deserialize<LoopScenario>(File.ReadAllText(path), Json)
                            ?? throw new InvalidOperationException($"Scenario '{path}' was empty."))
            .ToArray();
    }
}
