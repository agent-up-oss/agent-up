using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Interfaces;

namespace AgentUp.Desktop.Features.Capabilities.Services;

public sealed class CapabilityModulesCatalogService(ICapabilityModulesApiProvider client)
{
    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
        => client.ListAsync(cancellationToken);

    public Task<CapabilityModuleDto> EnableAsync(string id, CancellationToken cancellationToken = default)
        => client.EnableAsync(id, cancellationToken);

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
        => client.DisableAsync(id, cancellationToken);

    public Task<CapabilityModuleDto> UpdateSettingsAsync(
        string id,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
        => client.UpdateSettingsAsync(id, values, cancellationToken);
}
