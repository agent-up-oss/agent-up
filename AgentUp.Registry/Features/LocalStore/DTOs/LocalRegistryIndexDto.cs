namespace AgentUp.Registry.Features.LocalStore.DTOs;

public sealed record LocalRegistryIndexDto(IReadOnlyList<LocalRegistryIndexEntryDto> Packages);
