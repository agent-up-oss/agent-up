namespace AgentUp.Server.Features.Agents.Models;

/// <summary>Versioned pooled success rate for CPU-inference loop coverage.</summary>
public sealed record LoopOutcomeBaseline(
    string Model,
    double PooledSuccessRate,
    double Tolerance,
    int Samples,
    string RecordedAt);
