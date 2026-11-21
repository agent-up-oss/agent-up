using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Services;

namespace AgentUp.Desktop.Features.Capabilities.Controllers;

public sealed class CapabilityModulesController(CapabilityModulesCatalogService catalog)
{
    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
        => catalog.ListAsync(cancellationToken);

    public Task<CapabilityModuleDto> EnableAsync(string id, CancellationToken cancellationToken = default)
        => catalog.EnableAsync(id, cancellationToken);

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
        => catalog.DisableAsync(id, cancellationToken);

    public Task<CapabilityModuleDto> UpdateSettingsAsync(
        string id,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
        => catalog.UpdateSettingsAsync(id, values, cancellationToken);
}
