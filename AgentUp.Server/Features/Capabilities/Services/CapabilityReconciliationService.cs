using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Ports.Models;

namespace AgentUp.Server.Features.Capabilities.Services;

public sealed class CapabilityReconciliationService(IEnumerable<ICapabilityAdapter> adapters)
{
    private readonly IReadOnlyDictionary<string, ICapabilityAdapter> _adapters =
        adapters.ToDictionary(adapter => adapter.Descriptor.Id, StringComparer.OrdinalIgnoreCase);

    public async Task<ApplicationInstance> ReconcileDotnetAsync(
        DotnetApplicationDefinition definition,
        IReadOnlyList<PortDeclaration> ports,
        IReadOnlyList<PortMapping> allocatedPorts)
    {
        var requirements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(definition.Sdk))
            requirements["sdk"] = definition.Sdk;

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["project"] = definition.Run.Project
        };

        if (definition.Run.Arguments is { Count: > 0 })
            parameters["arguments"] = string.Join(" ", definition.Run.Arguments.Select(QuoteShellArgument));

        var resolved = await ReconcileAsync(new CapabilityDeclaration(definition.Name, "dotnet", requirements, parameters));
        return new ApplicationInstance
        {
            Name = definition.Name,
            Command = resolved.Plan.Command,
            Ports = ports,
            AllocatedPorts = allocatedPorts,
            CapabilityId = "dotnet",
            CapabilityVersionRequirement = definition.Sdk,
            CapabilityStatus = resolved.Status
        };
    }

    public async Task<ApplicationInstance> ReconcileDockerAsync(
        DockerCapabilityDefinition definition,
        IReadOnlyList<PortDeclaration> ports,
        IReadOnlyList<PortMapping> allocatedPorts)
    {
        var declaration = new CapabilityDeclaration(
            definition.Name,
            "docker",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["image"] = definition.Image });

        var resolved = await ReconcileAsync(declaration);
        return new ApplicationInstance
        {
            Name = definition.Name,
            ServiceType = ServiceType.Docker,
            Image = definition.Image,
            Ports = ports,
            AllocatedPorts = allocatedPorts,
            Environment = definition.Environment,
            Volumes = definition.Volumes,
            CapabilityId = "docker",
            CapabilityStatus = resolved.Status
        };
    }

    private async Task<ResolvedCapability> ReconcileAsync(CapabilityDeclaration declaration)
    {
        if (!_adapters.TryGetValue(declaration.CapabilityId, out var adapter))
        {
            var status = new CapabilityStatusDto(
                declaration.CapabilityId,
                null,
                false,
                [$"Capability '{declaration.CapabilityId}' is not installed."]);
            return new ResolvedCapability(new CapabilityLaunchPlan(""), status);
        }

        var installed = await adapter.DiscoverAsync(CancellationToken.None);
        var validation = await adapter.ValidateAsync(declaration, installed, CancellationToken.None);
        var plan = validation.CanRun
            ? await adapter.CreateLaunchPlanAsync(declaration, installed, CancellationToken.None)
            : new CapabilityLaunchPlan("");

        var requiredVersion = declaration.Requirements.TryGetValue("sdk", out var sdk) ? sdk : null;
        var statusDto = new CapabilityStatusDto(
            declaration.CapabilityId,
            requiredVersion,
            validation.CanRun,
            validation.Messages.Select(message => message.Message).ToList());

        return new ResolvedCapability(plan, statusDto);
    }

    private static string QuoteShellArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        return value.Any(char.IsWhiteSpace)
            ? "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\""
            : value;
    }

    private sealed record ResolvedCapability(CapabilityLaunchPlan Plan, CapabilityStatusDto Status);
}
