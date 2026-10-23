using AgentUp.CLI.Features.Capabilities.DTOs;

namespace AgentUp.CLI.Features.Capabilities.Interfaces;

public interface ICapabilityModulesApi
{
    Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<CapabilityModuleDto> EnableAsync(string id, string? version, CancellationToken cancellationToken = default);

    Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default);
}
