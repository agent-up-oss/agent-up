namespace AgentUp.Server.Features.Ports.Models;

public sealed record PortRangeData(
    int HighWaterMark,
    Dictionary<string, int> Ranges,
    List<int>? FreeRanges = null);
