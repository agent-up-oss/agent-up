using System.Text.Json;
using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Features.Applications.DTOs;

public sealed record RuntimeSectionDefinition(
    string ModuleId,
    IReadOnlyList<RuntimeSectionItem> Items)
{
    public static RuntimeSectionDefinition FromDotnet(IReadOnlyList<DotnetApplicationDefinition> applications)
        => new("dotnet", applications.Select(RuntimeSectionItem.FromDotnet).ToArray());

    public static RuntimeSectionDefinition FromDocker(IReadOnlyList<DockerCapabilityDefinition> applications)
        => new("docker", applications.Select(RuntimeSectionItem.FromDocker).ToArray());

    public static IReadOnlyList<RuntimeSectionDefinition> Merge(
        IReadOnlyList<RuntimeSectionDefinition>? sections,
        IReadOnlyList<DotnetApplicationDefinition>? dotnet,
        IReadOnlyList<DockerCapabilityDefinition>? docker)
    {
        var merged = new Dictionary<string, RuntimeSectionDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var section in sections ?? [])
            merged[section.ModuleId] = section;
        if (!merged.ContainsKey("dotnet") && dotnet is { Count: > 0 })
            merged["dotnet"] = FromDotnet(dotnet);
        if (!merged.ContainsKey("docker") && docker is { Count: > 0 })
            merged["docker"] = FromDocker(docker);
        return merged.Values.ToArray();
    }
}

public sealed record RuntimeSectionItem(
    string Name,
    string? TechnologyVersion = null,
    string? Path = null,
    IReadOnlyDictionary<string, string>? Parameters = null,
    IReadOnlyDictionary<string, string>? Environment = null,
    IReadOnlyList<string>? EnvironmentFiles = null,
    IReadOnlyList<PortDeclaration>? Ports = null,
    IReadOnlyList<string>? Volumes = null,
    IReadOnlyList<string>? ExtraArguments = null,
    bool Database = false,
    IReadOnlyDictionary<string, JsonElement>? Attributes = null)
{
    public static RuntimeSectionItem FromDotnet(DotnetApplicationDefinition definition)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["project"] = definition.Run.Project
        };
        var extra = definition.Run.Arguments ?? [];
        if (extra.Count > 0)
            parameters["arguments"] = JsonSerializer.Serialize(extra);
        return new RuntimeSectionItem(
            definition.Name,
            definition.Sdk,
            Parameters: parameters,
            Environment: definition.Environment,
            EnvironmentFiles: definition.EnvironmentFiles,
            Ports: definition.Ports,
            ExtraArguments: extra,
            Database: definition.Database,
            Attributes: AttributeMap(definition.Name, definition.Sdk, parameters, definition.Environment,
                definition.EnvironmentFiles, definition.Ports, null, extra, definition.Database));
    }

    public static RuntimeSectionItem FromDocker(DockerCapabilityDefinition definition)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image"] = definition.Image
        };
        if (definition.Volumes is { Count: > 0 })
            parameters["volumes"] = JsonSerializer.Serialize(definition.Volumes);
        if (definition.Command is { Count: > 0 })
            parameters["command"] = JsonSerializer.Serialize(definition.Command);
        return new RuntimeSectionItem(
            definition.Name,
            Parameters: parameters,
            Environment: definition.Environment,
            EnvironmentFiles: definition.EnvironmentFiles,
            Ports: definition.Ports,
            Volumes: definition.Volumes,
            ExtraArguments: definition.Command,
            Database: definition.Database,
            Attributes: AttributeMap(definition.Name, null, parameters, definition.Environment,
                definition.EnvironmentFiles, definition.Ports, definition.Volumes, definition.Command,
                definition.Database));
    }

    private static Dictionary<string, JsonElement> AttributeMap(
        string name,
        string? technologyVersion,
        IReadOnlyDictionary<string, string> parameters,
        IReadOnlyDictionary<string, string>? environment,
        IReadOnlyList<string>? environmentFiles,
        IReadOnlyList<PortDeclaration>? ports,
        IReadOnlyList<string>? volumes,
        IReadOnlyList<string>? extraArguments,
        bool database)
    {
        var attributes = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
        {
            ["name"] = JsonSerializer.SerializeToElement(name)
        };
        if (!string.IsNullOrWhiteSpace(technologyVersion))
            attributes["sdk"] = JsonSerializer.SerializeToElement(technologyVersion);
        if (environment is { Count: > 0 })
            attributes["environment"] = JsonSerializer.SerializeToElement(environment);
        if (environmentFiles is { Count: > 0 })
            attributes["environmentFiles"] = JsonSerializer.SerializeToElement(environmentFiles);
        if (ports is { Count: > 0 })
            attributes["ports"] = JsonSerializer.SerializeToElement(ports);
        if (volumes is { Count: > 0 })
            attributes["volumes"] = JsonSerializer.SerializeToElement(volumes);
        if (extraArguments is { Count: > 0 })
            attributes["arguments"] = JsonSerializer.SerializeToElement(extraArguments);
        if (database)
            attributes["database"] = JsonSerializer.SerializeToElement(true);
        foreach (var (key, value) in parameters)
        {
            if (!attributes.ContainsKey(key))
                attributes[key] = ParseOrString(value);
        }

        return attributes;
    }

    private static JsonElement ParseOrString(string value)
    {
        if (value.StartsWith('[') || value.StartsWith('{'))
        {
            try
            {
                return JsonSerializer.Deserialize<JsonElement>(value);
            }
            catch (JsonException)
            {
                return JsonSerializer.SerializeToElement(value);
            }
        }

        return JsonSerializer.SerializeToElement(value);
    }
}
