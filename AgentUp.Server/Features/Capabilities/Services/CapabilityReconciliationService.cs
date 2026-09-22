using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Registry.Features.Packages.Controllers;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Ports.DTOs;
using System.Text.Json;

namespace AgentUp.Server.Features.Capabilities.Services;

public sealed class CapabilityReconciliationService(
    IEnabledCapabilityPackages? packages = null,
    CapabilityPackageController? templates = null)
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

        var resolved = Reconcile(new CapabilityDeclaration(
            item.Name,
            moduleId,
            SdkRequirements(item.TechnologyVersion),
            parameters));
        return Task.FromResult(new ApplicationInstance
        {
            Name = item.Name,
            Path = item.Path,
            Command = resolved.Plan.Command,
            ServiceType = parameters.ContainsKey("image") ? ServiceType.Docker : ServiceType.Process,
            Image = parameters.GetValueOrDefault("image"),
            Ports = ports,
            AllocatedPorts = allocatedPorts,
            Environment = item.Environment,
            EnvironmentFiles = item.EnvironmentFiles,
            Volumes = volumes,
            Args = extra,
            CapabilityId = moduleId,
            CapabilityVersionRequirement = item.TechnologyVersion,
            CapabilityStatus = resolved.Status,
            Database = item.Database
        });
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

    private ResolvedCapability Reconcile(CapabilityDeclaration declaration)
    {
        var manifest = packages?.GetEnabled(declaration.CapabilityId);
        if (manifest is null)
        {
            var status = new CapabilityStatusDto(
                declaration.CapabilityId,
                RequiredVersion(declaration),
                false,
                [$"Capability '{declaration.CapabilityId}' is not enabled."]);
            return new ResolvedCapability(new CapabilityLaunchPlan(""), status);
        }

        var messages = ValidateParameters(manifest, declaration);
        var canRun = messages.Count == 0;
        var plan = canRun ? CreatePlan(manifest, declaration) : new CapabilityLaunchPlan("");
        return new ResolvedCapability(
            plan,
            new CapabilityStatusDto(declaration.CapabilityId, RequiredVersion(declaration), canRun, messages));
    }

    private CapabilityLaunchPlan CreatePlan(CapabilityPackageManifest manifest, CapabilityDeclaration declaration)
    {
        if (manifest.Launch is null)
            return new CapabilityLaunchPlan("");

        var values = new CapabilityTemplateValues(declaration.Parameters, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var command = templates is null ? manifest.Launch.Command : templates.Render(manifest.Launch.Command, values);
        var arguments = templates is null
            ? manifest.Launch.Arguments
            : templates.RenderAll(manifest.Launch.Arguments, values);
        var extra = ExtraArguments(declaration.Parameters);
        var rendered = extra.Count == 0 ? arguments : arguments.Concat(extra).ToArray();
        return new CapabilityLaunchPlan(string.Join(" ", new[] { command }.Concat(rendered)));
    }

    private static List<string> ValidateParameters(CapabilityPackageManifest manifest, CapabilityDeclaration declaration)
    {
        var messages = new List<string>();
        foreach (var (name, spec) in manifest.Parameters)
        {
            if (spec.Required && (!declaration.Parameters.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value)))
                messages.Add($"Capability '{manifest.Id}' requires parameter '{name}'.");
        }

        return messages;
    }

    private static IReadOnlyList<string> ExtraArguments(IReadOnlyDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("arguments", out var extra) || string.IsNullOrWhiteSpace(extra))
            return [];
        var parsed = ParseList(parameters, "arguments");
        return parsed ?? extra.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

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

    private static Dictionary<string, string> SdkRequirements(string? sdk)
    {
        var requirements = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(sdk))
            requirements["sdk"] = sdk;
        return requirements;
    }

    private static string? RequiredVersion(CapabilityDeclaration declaration)
        => declaration.Requirements.TryGetValue("sdk", out var sdk) ? sdk : null;
}
