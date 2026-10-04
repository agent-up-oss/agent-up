using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class McpWorkspaceSchemaProvider
{
    public IReadOnlyList<string> WorkspaceIdPropertyNames(JsonElement schema)
        => PropertyNames(schema, McpWorkspaceTargetArguments.IsWorkspaceIdName);

    /// <summary>
    /// Every property that names the workspace a tool acts on: the id parameters and the
    /// Server-host path parameters. A workspace-bound session is told neither, because the
    /// binder supplies them, so both are removed from what it is offered.
    /// </summary>
    public IReadOnlyList<string> WorkspaceTargetPropertyNames(JsonElement schema)
        => PropertyNames(
            schema,
            (name, description) => McpWorkspaceTargetArguments.IsWorkspaceIdName(name, description)
                                   || McpWorkspaceTargetArguments.IsPathName(name));

    public JsonElement StripWorkspaceIdProperties(JsonElement schema)
        => Strip(schema, WorkspaceIdPropertyNames(schema));

    public JsonElement StripWorkspaceTargetProperties(JsonElement schema)
        => Strip(schema, WorkspaceTargetPropertyNames(schema));

    public static string? ReadDescription(JsonElement property)
    {
        if (property.ValueKind is not JsonValueKind.Object)
            return null;
        if (!property.TryGetProperty("description", out var description))
            return null;
        if (description.ValueKind is not JsonValueKind.String)
            return null;
        return description.GetString();
    }

    private static IReadOnlyList<string> PropertyNames(JsonElement schema, Func<string, string?, bool> matches)
    {
        if (schema.ValueKind is not JsonValueKind.Object)
            return [];
        if (!schema.TryGetProperty("properties", out var properties))
            return [];
        if (properties.ValueKind is not JsonValueKind.Object)
            return [];

        return properties.EnumerateObject()
            .Where(property => matches(property.Name, ReadDescription(property.Value)))
            .Select(property => property.Name)
            .ToArray();
    }

    private static JsonElement Strip(JsonElement schema, IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            return schema;

        var root = JsonNode.Parse(schema.GetRawText())!.AsObject();
        var properties = root["properties"]!.AsObject();
        foreach (var name in names)
            properties.Remove(name);

        if (root["required"] is JsonArray required)
        {
            for (var index = required.Count - 1; index >= 0; index--)
            {
                if (names.Contains(required[index]?.GetValue<string>(), StringComparer.OrdinalIgnoreCase))
                    required.RemoveAt(index);
            }
        }

        return JsonSerializer.SerializeToElement(root);
    }
}
