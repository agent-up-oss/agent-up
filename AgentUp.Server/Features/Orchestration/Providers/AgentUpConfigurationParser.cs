using System.Text.Json;
using AgentUp.Sdk.Runtime;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Ports.DTOs;

namespace AgentUp.Server.Features.Orchestration.Providers;

public static class AgentUpConfigurationParser
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "name",
        "display",
        "applications",
        "desktopApplications",
        "services",
        "prompts",
        "commits",
        "verification",
        "coverage"
    };

    public static AgentUpConfiguration Parse(
        JsonElement root,
        IReadOnlyList<IRuntimeCapability> runtimes,
        JsonSerializerOptions options)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("agent-up.json must be a JSON object.");

        var name = ReadString(root, "name");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("agent-up.json requires 'name'.");

        var sections = new List<RuntimeSectionDefinition>();
        foreach (var property in root.EnumerateObject().Where(property => !Reserved.Contains(property.Name)))
        {
            var runtime = MatchRuntime(runtimes, property.Name);
            if (property.Value.ValueKind != JsonValueKind.Array)
            {
                if (runtime is not null)
                    throw new InvalidOperationException($"Runtime section '{property.Name}' must be an array.");
                continue;
            }

            var attributes = property.Value.EnumerateArray()
                .Select(ReadObject)
                .Select(RuntimeSectionBinder.PromoteNestedRun)
                .ToArray();

            // No enabled module claims this section. Keep it anyway: the workspace still owns
            // these applications, and dropping them here silently deletes the ones the CLI
            // registered, because starting a workspace re-registers it from this file.
            // Reconciliation reports them unrunnable when nothing can host them.
            if (runtime is null)
            {
                sections.Add(new RuntimeSectionDefinition(
                    property.Name,
                    attributes.Select(item => ToItem(item, Flatten(item), options)).ToArray()));
                continue;
            }

            var bind = runtime.Bind(attributes);
            if (!bind.IsValid)
            {
                throw new InvalidOperationException(
                    $"Runtime section '{runtime.Identity.Id}': {string.Join(" ", bind.Messages)}");
            }

            sections.Add(new RuntimeSectionDefinition(
                runtime.Identity.Id,
                attributes.Select((item, index) => ToItem(item, bind.Items[index], options)).ToArray()));
        }

        return new AgentUpConfiguration(
            name,
            Read<IReadOnlyList<ApplicationDefinition>>(root, "applications", options),
            Read<IReadOnlyList<DesktopApplicationDefinition>>(root, "desktopApplications", options),
            Read<IReadOnlyList<DockerServiceDefinition>>(root, "services", options),
            MapDotnet(sections),
            MapDocker(sections),
            sections,
            Read<AgentPromptConfiguration>(root, "prompts", options),
            Read<WorkspaceDisplayConfiguration>(root, "display", options));
    }

    private static IRuntimeCapability? MatchRuntime(IReadOnlyList<IRuntimeCapability> runtimes, string name)
        => runtimes.FirstOrDefault(runtime =>
            runtime.Identity.Id.Equals(name, StringComparison.OrdinalIgnoreCase)
            || runtime.SectionName.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<DotnetApplicationDefinition>? MapDotnet(IReadOnlyList<RuntimeSectionDefinition> sections)
    {
        var section = sections.FirstOrDefault(item => item.ModuleId.Equals("dotnet", StringComparison.OrdinalIgnoreCase));
        return section?.Items.Select(item => new DotnetApplicationDefinition(
            item.Name,
            item.TechnologyVersion,
            new DotnetRunDefinition(
                item.Parameters?.GetValueOrDefault("project") ?? "",
                item.ExtraArguments),
            item.Ports,
            item.Environment,
            item.EnvironmentFiles,
            item.Database)).ToArray();
    }

    private static IReadOnlyList<DockerCapabilityDefinition>? MapDocker(IReadOnlyList<RuntimeSectionDefinition> sections)
    {
        var section = sections.FirstOrDefault(item => item.ModuleId.Equals("docker", StringComparison.OrdinalIgnoreCase));
        return section?.Items.Select(item => new DockerCapabilityDefinition(
            item.Name,
            item.Parameters?.GetValueOrDefault("image") ?? "",
            item.Ports,
            item.Environment,
            item.Volumes,
            item.EnvironmentFiles,
            item.ExtraArguments,
            item.Database)).ToArray();
    }

    private static RuntimeSectionItem ToItem(
        IReadOnlyDictionary<string, JsonElement> attributes,
        IReadOnlyDictionary<string, string> bound,
        JsonSerializerOptions options)
    {
        var extra = ReadStringList(attributes, "arguments")
                    ?? ReadStringList(attributes, "command")
                    ?? [];
        return new RuntimeSectionItem(
            ReadBound(bound, "name") ?? "application",
            ReadBound(bound, RuntimeCommonAttributes.TechnologyVersion),
            ReadBound(bound, RuntimeCommonAttributes.Path),
            bound,
            ReadObjectStrings(attributes, RuntimeCommonAttributes.Environment, options),
            ReadStringList(attributes, RuntimeCommonAttributes.EnvironmentFiles),
            Read<IReadOnlyList<PortDeclaration>>(attributes, RuntimeCommonAttributes.Ports, options),
            ReadStringList(attributes, "volumes"),
            extra,
            ReadBool(attributes, RuntimeCommonAttributes.Database),
            attributes);
    }

    /// <summary>The attributes as plain strings, standing in for a module's bound values.</summary>
    private static IReadOnlyDictionary<string, string> Flatten(IReadOnlyDictionary<string, JsonElement> attributes)
    {
        var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in attributes)
        {
            flattened[key] = value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? "",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => "",
                _ => value.GetRawText()
            };
        }

        return flattened;
    }

    private static Dictionary<string, JsonElement> ReadObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Runtime section items must be JSON objects.");

        var item = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            item[property.Name] = property.Value.Clone();
        return item;
    }

    private static T? Read<T>(JsonElement root, string name, JsonSerializerOptions options)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return default;
        return value.Deserialize<T>(options);
    }

    private static T? Read<T>(IReadOnlyDictionary<string, JsonElement> attributes, string name, JsonSerializerOptions options)
        => attributes.TryGetValue(name, out var value) ? value.Deserialize<T>(options) : default;

    private static IReadOnlyDictionary<string, string>? ReadObjectStrings(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        JsonSerializerOptions options)
        => Read<Dictionary<string, string>>(attributes, name, options);

    private static IReadOnlyList<string>? ReadStringList(IReadOnlyDictionary<string, JsonElement> attributes, string name)
    {
        if (!attributes.TryGetValue(name, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;
        if (value.ValueKind == JsonValueKind.Array)
            return value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() ?? "" : item.GetRawText()).ToArray();
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();
            return string.IsNullOrWhiteSpace(text) ? [] : [text];
        }

        return [value.GetRawText()];
    }

    private static bool ReadBool(IReadOnlyDictionary<string, JsonElement> attributes, string name)
        => attributes.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? ReadBound(IReadOnlyDictionary<string, string> bound, string name)
        => bound.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static string? ReadString(JsonElement root, string name)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
