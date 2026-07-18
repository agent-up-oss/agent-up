namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityLaunchPlan(
    string Command,
    string? WorkingDirectory = null,
    IReadOnlyDictionary<string, string>? Environment = null);
