using AgentUp.Desktop.Features.Capabilities.DTOs;

namespace AgentUp.Desktop.Features.Capabilities.Interfaces;

public interface ICapabilityModulesApiProvider
{
    Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CapabilityModuleDto> EnableAsync(string id, string? version, CancellationToken cancellationToken = default);

    Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default);
}
