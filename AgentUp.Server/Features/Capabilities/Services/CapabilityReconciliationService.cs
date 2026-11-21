using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Capabilities.Models;
using AgentUp.Server.Features.Ports.DTOs;
using System.Text.Json;

namespace AgentUp.Server.Features.Capabilities.Services;

/// <summary>
/// Turns one <c>agent-up.json</c> runtime-section item into an application the Server can launch,
/// through the built-in runtime capability that owns the section.
/// </summary>
public sealed class CapabilityReconciliationService(IEnabledCapabilityPackages? packages = null)
{
    public Task<ApplicationInstance> ReconcileDotnetAsync(
        DotnetApplicationDefinition definition,
        IReadOnlyList<PortDeclaration> ports,
        IReadOnlyList<PortMapping> allocatedPorts)
        => ReconcileRuntimeAsync("dotnet", RuntimeSectionItem.FromDotnet(definition), ports, allocatedPorts);

    public Task<ApplicationInstance> ReconcileDockerAsync(
        DockerCapabilityDefinition definition,
        IReadOnlyList<PortDeclaration> ports,
        IReadOnlyList<PortMapping> allocatedPorts)
        => ReconcileRuntimeAsync("docker", RuntimeSectionItem.FromDocker(definition), ports, allocatedPorts);

    public Task<ApplicationInstance> ReconcileRuntimeAsync(
        string moduleId,
        RuntimeSectionItem item,
        IReadOnlyList<PortDeclaration> ports,
        IReadOnlyList<PortMapping> allocatedPorts)
    {
        var blocked = Blocked(moduleId, item.Name);
        if (blocked is not null)
        {
            return Task.FromResult(Unrunnable(
                moduleId,
                item,
                ports,
                allocatedPorts,
                blocked));
        }

        var bound = Bind(moduleId, item);
        if (bound is { IsValid: false })
        {
            return Task.FromResult(Unrunnable(
                moduleId,
                item,
                ports,
                allocatedPorts,
                bound.Messages));
        }

        var parameters = bound?.Items.Count > 0
            ? new Dictionary<string, string>(bound.Items[0], StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(item.Parameters ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
        var extra = item.ExtraArguments ?? ParseList(parameters, "arguments") ?? ParseList(parameters, "command") ?? [];
        var volumes = item.Volumes ?? ParseList(parameters, "volumes") ?? [];
        var runtime = packages?.GetRuntime(moduleId);
        if (runtime is not null)
        {
            if (IsContainer(runtime, parameters))
            {
                var delivered = DeliverRuntime(moduleId, item.TechnologyVersion);
                return Task.FromResult(new ApplicationInstance
                {
                    Name = item.Name,
                    Path = item.Path,
                    ServiceType = ServiceType.Docker,
                    Image = parameters.GetValueOrDefault("image"),
                    Ports = ports,
                    AllocatedPorts = allocatedPorts,
                    Environment = item.Environment,
                    EnvironmentFiles = item.EnvironmentFiles,
                    Volumes = volumes,
                    Args = extra,
                    CapabilityId = moduleId,
                    CapabilityVersionRequirement = item.TechnologyVersion,
                    CapabilityStatus = delivered ?? new CapabilityStatusDto(moduleId, item.TechnologyVersion, true, []),
                    Database = item.Database
                });
            }

            var hosted = HostRuntime(moduleId, item.Name, item.TechnologyVersion, parameters, extra, item.Environment, volumes);
            if (hosted is not null)
            {
                return Task.FromResult(new ApplicationInstance
                {
                    Name = item.Name,
                    Path = item.Path,
                    Command = Join(hosted.FileName, hosted.Arguments),
                    LaunchFileName = hosted.FileName,
                    LaunchArguments = hosted.Arguments,
                    Ports = ports,
                    AllocatedPorts = allocatedPorts,
                    Environment = item.Environment,
                    EnvironmentFiles = item.EnvironmentFiles,
                    Volumes = volumes,
                    CapabilityId = moduleId,
                    CapabilityVersionRequirement = item.TechnologyVersion,
                    CapabilityStatus = hosted.Status,
                    Database = item.Database
                });
            }
        }

        return Task.FromResult(Unrunnable(
            moduleId,
            item,
            ports,
            allocatedPorts,
            CapabilityPrerequisiteMessages.NotEnabled(moduleId, item.Name)));
    }

    private RuntimeBindResult? Bind(string moduleId, RuntimeSectionItem item)
    {
        var runtime = packages?.GetRuntime(moduleId);
        if (runtime is null)
            return null;
        var attributes = item.Attributes ?? new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (attributes.Count == 0)
            return new RuntimeBindResult(true, [], [item.Parameters ?? new Dictionary<string, string>()]);
        return runtime.Bind([attributes]);
    }

    private static ApplicationInstance Unrunnable(
        string moduleId,
        RuntimeSectionItem item,
        IReadOnlyList<PortDeclaration> ports,
        IReadOnlyList<PortMapping> allocatedPorts,
        IReadOnlyList<string> messages)
        => new()
        {
            Name = item.Name,
            Path = item.Path,
            Command = "",
            CapabilityId = moduleId,
            CapabilityVersionRequirement = item.TechnologyVersion,
            CapabilityStatus = new CapabilityStatusDto(moduleId, item.TechnologyVersion, false, messages),
            Ports = ports,
            AllocatedPorts = allocatedPorts,
            Environment = item.Environment,
            EnvironmentFiles = item.EnvironmentFiles,
            Volumes = item.Volumes,
            Args = item.ExtraArguments,
            Image = item.Parameters?.GetValueOrDefault("image"),
            ServiceType = item.Parameters?.ContainsKey("image") == true ? ServiceType.Docker : ServiceType.Process,
            Database = item.Database
        };

    private HostedRuntime? HostRuntime(
        string id,
        string name,
        string? technologyVersion,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyList<string> extraArguments,
        IReadOnlyDictionary<string, string>? environment = null,
        IReadOnlyList<string>? volumes = null,
        IReadOnlyList<RuntimePortMapping>? ports = null)
    {
        var runtime = packages?.GetRuntime(id);
        if (runtime is null)
            return null;

        var delivered = runtime.Deliver(technologyVersion);
        var host = runtime.Host(new RuntimeHostRequest(
            name,
            technologyVersion,
            parameters,
            environment ?? new Dictionary<string, string>(),
            ports ?? [],
            volumes ?? [],
            extraArguments,
            "",
            name));
        var canRun = delivered.CanDeliver && host.CanRun;
        var messages = delivered.Messages.Concat(host.Messages).ToArray();
        if (!canRun)
        {
            return new HostedRuntime(
                "",
                [],
                new CapabilityStatusDto(id, technologyVersion, false, messages.Length == 0 ? [$"Capability '{id}' cannot host '{name}'."] : messages));
        }

        return new HostedRuntime(
            host.FileName,
            host.Arguments,
            new CapabilityStatusDto(id, technologyVersion, true, []));
    }

    private CapabilityStatusDto? DeliverRuntime(string id, string? technologyVersion)
    {
        var runtime = packages?.GetRuntime(id);
        if (runtime is null)
            return null;

        var delivered = runtime.Deliver(technologyVersion);
        return new CapabilityStatusDto(
            id,
            technologyVersion,
            delivered.CanDeliver,
            delivered.CanDeliver ? [] : delivered.Messages);
    }

    private static bool IsContainer(IRuntimeCapability runtime, IReadOnlyDictionary<string, string> parameters)
        => runtime.ExtraAttributes.Any(spec => spec.Name.Equals("image", StringComparison.OrdinalIgnoreCase))
           || parameters.ContainsKey("image");

    private static string Join(string fileName, IReadOnlyList<string> arguments)
        => string.Join(" ", new[] { fileName }.Concat(arguments));

    private static IReadOnlyList<string>? ParseList(IReadOnlyDictionary<string, string> parameters, string name)
    {
        if (!parameters.TryGetValue(name, out var raw) || string.IsNullOrWhiteSpace(raw))
            return null;
        raw = raw.Trim();
        if (raw.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<string[]>(raw);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Why the runtime cannot host this item at all: its software is missing, or it is not enabled.
    /// Software comes first, because enabling cannot help until it is found.
    /// </summary>
    private IReadOnlyList<string>? Blocked(string moduleId, string applicationName)
    {
        var readiness = packages?.Readiness(moduleId);
        if (readiness is null)
            return CapabilityPrerequisiteMessages.NotEnabled(moduleId, applicationName);
        if (!readiness.Available)
            return CapabilityPrerequisiteMessages.SoftwareMissing(moduleId, readiness.MissingSoftware);
        return readiness.Enabled ? null : CapabilityPrerequisiteMessages.NotEnabled(moduleId, applicationName);
    }
}
