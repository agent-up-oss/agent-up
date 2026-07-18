using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Interfaces;

namespace AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

public sealed class DockerCapabilityAdapter(IDockerVersionProvider versions) : ICapabilityAdapter
{
    public CapabilityDescriptor Descriptor { get; } =
        new("docker", "Docker", "1.0.0", true, ["linux", "macos", "windows"]);

    public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
        versions.DiscoverAsync(cancellationToken);

    public Task<CapabilityValidationResult> ValidateAsync(
        CapabilityDeclaration declaration,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions,
        CancellationToken cancellationToken)
    {
        var messages = new List<CapabilityValidationMessage>();
        if (!declaration.Parameters.TryGetValue("image", out var image) || string.IsNullOrWhiteSpace(image))
        {
            messages.Add(new CapabilityValidationMessage("docker.image.required", "A Docker capability declaration requires an image.", CapabilityValidationSeverity.Error));
        }

        if (installedVersions.Count == 0)
        {
            messages.Add(new CapabilityValidationMessage("docker.cli.missing", "Docker CLI is not installed or not reachable.", CapabilityValidationSeverity.Error));
        }

        var result = messages.Any(message => message.Severity == CapabilityValidationSeverity.Error)
            ? CapabilityValidationResult.Failure([.. messages])
            : CapabilityValidationResult.Success([.. messages]);

        return Task.FromResult(result);
    }

    public Task<CapabilityLaunchPlan> CreateLaunchPlanAsync(
        CapabilityDeclaration declaration,
        IReadOnlyList<CapabilityInstalledVersion> installedVersions,
        CancellationToken cancellationToken)
    {
        var image = declaration.Parameters["image"];
        return Task.FromResult(new CapabilityLaunchPlan($"docker run {image}"));
    }
}
