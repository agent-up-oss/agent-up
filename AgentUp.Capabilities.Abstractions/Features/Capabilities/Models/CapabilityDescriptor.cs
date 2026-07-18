namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityDescriptor(
    string Id,
    string DisplayName,
    string AdapterVersion,
    bool IsFirstParty,
    IReadOnlyList<string> SupportedPlatforms);
