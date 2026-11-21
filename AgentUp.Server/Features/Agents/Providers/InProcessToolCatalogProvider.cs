using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Shapes MCP tool descriptors and results for an in-process agent. A tool is offered only when
/// it names its workspace through <c>workspaceId</c>; that parameter (and <c>worktreePath</c>)
/// is removed from what the model sees and supplied by the host, so the model has no way to
/// address another team member's workspace.
/// </summary>
public sealed class InProcessToolCatalogProvider
{
    public const string WorkspaceParameter = "workspaceId";
    public const string WorktreeParameter = "worktreePath";

    public InProcessTool? Describe(JsonElement tool)
    {
        if (!tool.TryGetProperty("name", out var nameElement) || nameElement.GetString() is not { Length: > 0 } name)
            return null;
        if (!tool.TryGetProperty("inputSchema", out var schema) || schema.ValueKind != JsonValueKind.Object)
            return null;
        var node = JsonNode.Parse(schema.GetRawText())!.AsObject();
        if (node["properties"] is not JsonObject properties || !properties.ContainsKey(WorkspaceParameter))
            return null;
        properties.Remove(WorkspaceParameter);
        properties.Remove(WorktreeParameter);
        if (node["required"] is JsonArray required)
        {
            foreach (var pinned in required.Where(item => item?.GetValue<string>() is WorkspaceParameter or WorktreeParameter).ToArray())
                required.Remove(pinned);
        }

        var description = tool.TryGetProperty("description", out var text) ? text.GetString() ?? "" : "";
        return new InProcessTool(name, description, JsonSerializer.SerializeToElement(node));
    }

    /// <summary>The model's arguments with the workspace forced to the host's own.</summary>
    public IReadOnlyDictionary<string, JsonElement> Pin(JsonElement arguments, string workspaceId)
    {
        var pinned = arguments.ValueKind == JsonValueKind.Object
            ? arguments.EnumerateObject()
                .Where(property => !property.NameEquals(WorkspaceParameter) && !property.NameEquals(WorktreeParameter))
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        pinned[WorkspaceParameter] = JsonSerializer.SerializeToElement(workspaceId);
        return pinned;
    }

    public InProcessToolResult Read(JsonElement result)
    {
        var text = new List<string>();
        var images = new List<string>();
        if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                var type = block.TryGetProperty("type", out var kind) ? kind.GetString() : null;
                if (type == "text" && block.TryGetProperty("text", out var value))
                    text.Add(value.GetString() ?? "");
                else if (type == "image" && block.TryGetProperty("data", out var data) && data.GetString() is { Length: > 0 } image)
                    images.Add(image);
            }
        }

        if (text.Count == 0 && result.TryGetProperty("structuredContent", out var structured))
            text.Add(structured.GetRawText());
        var isError = result.TryGetProperty("isError", out var error) && error.ValueKind == JsonValueKind.True;
        return new InProcessToolResult(string.Join("\n", text), images, isError);
    }
}
