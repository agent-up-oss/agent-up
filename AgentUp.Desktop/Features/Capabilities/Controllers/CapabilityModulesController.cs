using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Services;

namespace AgentUp.Desktop.Features.Capabilities.Controllers;

public sealed class CapabilityModulesController(CapabilityModulesCatalogService catalog)
{
    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
        => catalog.ListAsync(cancellationToken);

    public Task<CapabilityModuleDto> EnableAsync(
        string id,
        string? version,
        CancellationToken cancellationToken = default)
        => catalog.EnableAsync(id, version, cancellationToken);

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
        => catalog.DisableAsync(id, cancellationToken);
}
