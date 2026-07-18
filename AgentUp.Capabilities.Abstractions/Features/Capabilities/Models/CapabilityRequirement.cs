namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityRequirement(string Name, string? VersionRange = null);
