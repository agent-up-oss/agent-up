namespace AgentUp.Registry.Features.LocalStore.DTOs;

public sealed record LocalRegistryIndexEntryDto(
    string Id,
    string Version,
    string DisplayName,
    string Publisher,
    string Kind);
