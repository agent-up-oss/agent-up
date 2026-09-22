using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.DTOs;

namespace AgentUp.CLI.Features.Workspaces.Providers;

public static class AgentUpJsonParser
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

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

    public static AgentUpJson Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("agent-up.json must be a JSON object.");

        var name = ReadString(root, "name");
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("agent-up.json requires 'name'.");

        var sections = ReadRuntimeSections(root);
        return new AgentUpJson(
            name,
            Read<List<ApplicationDefinition>>(root, "applications"),
            Read<List<DesktopApplicationDefinition>>(root, "desktopApplications"),
            Read<List<DockerServiceDefinition>>(root, "services"),
            MapDotnet(sections),
            MapDocker(sections),
            sections,
            Read<WorkspaceDisplayOptions>(root, "display"));
    }

    private static List<RuntimeSectionDefinition> ReadRuntimeSections(JsonElement root)
        => root.EnumerateObject()
            .Where(property => !Reserved.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.Array)
            .Select(property => new RuntimeSectionDefinition(
                property.Name,
                property.Value.EnumerateArray().Select(ReadItem).ToArray()))
            .ToList();

    private static RuntimeSectionItem ReadItem(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Runtime section items must be JSON objects.");

        var attributes = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            attributes[property.Name] = property.Value.Clone();
        attributes = PromoteNestedRun(attributes);

        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in attributes)
            parameters[key] = Flatten(value);

        var extra = ReadStringList(attributes, "arguments")
                    ?? ReadStringList(attributes, "command")
                    ?? [];
        return new RuntimeSectionItem(
            ReadString(attributes, "name") ?? "application",
            ReadString(attributes, "sdk"),
            ReadString(attributes, "path"),
            parameters,
            ReadObjectStrings(attributes, "environment"),
            ReadStringList(attributes, "environmentFiles"),
            Read<List<PortDeclaration>>(attributes, "ports")?.ToArray(),
            ReadStringList(attributes, "volumes"),
            extra,
            ReadBool(attributes, "database"),
            attributes);
    }

    private static List<DotnetApplicationDefinition>? MapDotnet(IReadOnlyList<RuntimeSectionDefinition> sections)
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
            item.Database)).ToList();
    }

    private static List<DockerCapabilityDefinition>? MapDocker(IReadOnlyList<RuntimeSectionDefinition> sections)
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
            item.Database)).ToList();
    }

    private static Dictionary<string, JsonElement> PromoteNestedRun(Dictionary<string, JsonElement> item)
    {
        if (!item.TryGetValue("run", out var run) || run.ValueKind != JsonValueKind.Object)
            return item;

        foreach (var property in run.EnumerateObject())
        {
            if (property.Name.Equals("project", StringComparison.OrdinalIgnoreCase) && !HasValue(item, "project"))
                item["project"] = property.Value.Clone();
            if (property.Name.Equals("arguments", StringComparison.OrdinalIgnoreCase) && !HasValue(item, "arguments"))
                item["arguments"] = property.Value.Clone();
        }

        return item;
    }

    private static bool HasValue(IReadOnlyDictionary<string, JsonElement> item, string name)
        => item.TryGetValue(name, out var value)
           && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
           && (value.ValueKind != JsonValueKind.String || !string.IsNullOrWhiteSpace(value.GetString()));

    private static T? Read<T>(JsonElement root, string name)
        => TryGetProperty(root, name, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            ? value.Deserialize<T>(Json)
            : default;

    private static T? Read<T>(IReadOnlyDictionary<string, JsonElement> attributes, string name)
        => attributes.TryGetValue(name, out var value) ? value.Deserialize<T>(Json) : default;

    private static IReadOnlyDictionary<string, string>? ReadObjectStrings(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name)
        => Read<Dictionary<string, string>>(attributes, name);

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

    private static string? ReadString(JsonElement root, string name)
        => TryGetProperty(root, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement> attributes, string name)
        => attributes.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryGetProperty(JsonElement root, string name, out JsonElement value)
    {
        var match = root.EnumerateObject()
            .FirstOrDefault(property => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match.Value.ValueKind is JsonValueKind.Undefined)
        {
            value = default;
            return false;
        }

        value = match.Value;
        return true;
    }

    private static string Flatten(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Null => "",
            _ => value.GetRawText()
        };
}
