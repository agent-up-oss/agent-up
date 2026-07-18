using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;

public interface ICapabilityAdapter
{
    CapabilityDescriptor Descriptor { get; }

    Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken);

    Task<CapabilityValidationResult> ValidateAsync(
        CapabilityDeclaration declaration,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions,
        CancellationToken cancellationToken);

    Task<CapabilityLaunchPlan> CreateLaunchPlanAsync(
        CapabilityDeclaration declaration,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions,
        CancellationToken cancellationToken);
}
