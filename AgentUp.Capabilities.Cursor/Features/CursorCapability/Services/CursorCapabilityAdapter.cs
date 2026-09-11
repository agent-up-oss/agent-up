using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Interfaces;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

public sealed class CursorCapabilityAdapter(ICursorVersionProvider versions) : ICapabilityAdapter
{
    public CapabilityDescriptor Descriptor { get; } =
        new("cursor", "Cursor", "1.0.0", true, ["linux", "macos", "windows"]);

    public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
        versions.DiscoverAsync(cancellationToken);

    public Task<CapabilityValidationResult> ValidateAsync(
        CapabilityDeclaration declaration,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions,
        CancellationToken cancellationToken)
    {
        if (installedVersions.Count == 0)
        {
            return Task.FromResult(CapabilityValidationResult.Failure(
                new CapabilityValidationMessage(
                    "cursor.cli.missing",
                    "Cursor ACP CLI is not installed or not reachable.",
                    CapabilityValidationSeverity.Error)));
        }

        return Task.FromResult(CapabilityValidationResult.Success());
    }

    public Task<CapabilityLaunchPlan> CreateLaunchPlanAsync(
        CapabilityDeclaration declaration,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions,
        CancellationToken cancellationToken)
    {
        var launch = versions.ResolveLaunch(installedVersions);
        return Task.FromResult(new CapabilityLaunchPlan(launch.FileName, Arguments: launch.Arguments));
    }
}
