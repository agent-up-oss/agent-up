namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityDeclaration(
    string Name,
    string CapabilityId,
    IReadOnlyDictionary<string, string> Requirements,
    IReadOnlyDictionary<string, string> Parameters);
