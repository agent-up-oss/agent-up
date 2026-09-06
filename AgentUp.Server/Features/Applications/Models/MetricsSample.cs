namespace AgentUp.Server.Features.Applications.Models;

internal sealed record MetricsSample(
    DateTimeOffset Timestamp,
    IReadOnlyDictionary<string, double> Values);
