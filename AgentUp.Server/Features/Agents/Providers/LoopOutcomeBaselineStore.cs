using System.Text.Json;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Compares a pooled outcome rate to a versioned baseline with a declared relative tolerance,
/// the same shape as the performance gates.
/// </summary>
public sealed class LoopOutcomeBaselineStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public LoopOutcomeBaseline Read(string path)
    {
        if (!File.Exists(path))
            throw new InvalidOperationException($"Loop-coverage baseline was not found at '{path}'.");
        return JsonSerializer.Deserialize<LoopOutcomeBaseline>(File.ReadAllText(path), Json)
               ?? throw new InvalidOperationException($"Loop-coverage baseline '{path}' was empty.");
    }

    public bool WithinTolerance(LoopOutcomeBaseline baseline, double observedRate)
    {
        var floor = baseline.PooledSuccessRate * (1 - baseline.Tolerance);
        return observedRate + 1e-9 >= floor;
    }
}
