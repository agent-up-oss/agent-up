namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityTemplateValues(
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyDictionary<string, string> Environment);
