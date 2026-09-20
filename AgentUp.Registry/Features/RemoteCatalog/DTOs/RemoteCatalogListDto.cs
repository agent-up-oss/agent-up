using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Registry.Features.RemoteCatalog.DTOs;

public sealed record RemoteCatalogListDto(IReadOnlyList<CapabilityRegistryIndexEntry> Packages);
