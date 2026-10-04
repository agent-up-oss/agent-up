using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class McpWorkspaceSchemaProvider
{
    public IReadOnlyList<string> WorkspaceIdPropertyNames(JsonElement schema)
    {
        if (schema.ValueKind is not JsonValueKind.Object
            || !schema.TryGetProperty("properties", out var properties)
            || properties.ValueKind is not JsonValueKind.Object)
            return [];

        return properties.EnumerateObject()
            .Where(property => McpWorkspaceTargetArguments.IsWorkspaceIdName(property.Name, ReadDescription(property.Value)))
            .Select(property => property.Name)
            .ToArray();
    }

    public JsonElement StripWorkspaceIdProperties(JsonElement schema)
    {
        if (schema.ValueKind is not JsonValueKind.Object)
            return schema;

        var names = WorkspaceIdPropertyNames(schema);
        if (names.Count == 0)
            return schema;

        var node = JsonNode.Parse(schema.GetRawText());
        if (node is not JsonObject root || root["properties"] is not JsonObject properties)
            return schema;

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

    private static string? ReadDescription(JsonElement property)
        => property.ValueKind is JsonValueKind.Object
           && property.TryGetProperty("description", out var description)
           && description.ValueKind is JsonValueKind.String
            ? description.GetString()
            : null;
}
