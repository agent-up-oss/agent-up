using System.Text.Json;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Services;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class McpWorkspaceBindingFilterProvider(
    McpWorkspaceArgumentBinder binder,
    McpWorkspaceSchemaProvider schemas) : IMcpWorkspaceBindingFilterProvider
{
    public void Attach(McpServerOptions options, string boundWorkspace)
    {
        options.Filters.Request.ListToolsFilters.Add(next => (context, cancellationToken) =>
            HideWorkspaceParametersAsync(next, context, cancellationToken));
        options.Filters.Request.CallToolFilters.Add(next => (context, cancellationToken) =>
            BindCallAsync(next, context, boundWorkspace, cancellationToken));
    }

    private async ValueTask<ListToolsResult> HideWorkspaceParametersAsync(
        McpRequestHandler<ListToolsRequestParams, ListToolsResult> next,
        RequestContext<ListToolsRequestParams> context,
        CancellationToken cancellationToken)
    {
        var result = await next(context, cancellationToken);
        if (result.Tools is null)
            return result;

        result.Tools = result.Tools.Select(HideWorkspaceParameters).ToList();
        return result;
    }

    private Tool HideWorkspaceParameters(Tool tool)
        => new()
        {
            Name = tool.Name,
            Title = tool.Title,
            Description = tool.Description,
            InputSchema = schemas.StripWorkspaceIdProperties(tool.InputSchema),
            OutputSchema = tool.OutputSchema,
            Annotations = tool.Annotations,
            Icons = tool.Icons,
            Meta = tool.Meta
        };

    private async ValueTask<CallToolResult> BindCallAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        string boundWorkspace,
        CancellationToken cancellationToken)
    {
        var schema = context.MatchedPrimitive is McpServerTool tool
            ? tool.ProtocolTool.InputSchema
            : default;
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        if (context.Params?.Arguments is { } supplied)
        {
            foreach (var (key, value) in supplied)
                arguments[key] = value;
        }
        var decision = await binder.BindAsync(
            boundWorkspace,
            arguments,
            schemas.WorkspaceIdPropertyNames(schema),
            cancellationToken);
        if (!decision.Allowed)
            return Forbid(decision.Error);

        if (context.Params is not null)
            context.Params.Arguments = new Dictionary<string, JsonElement>(decision.Arguments, StringComparer.OrdinalIgnoreCase);

        var result = await next(context, cancellationToken);
        if (string.Equals(context.Params?.Name, "list_workspaces", StringComparison.Ordinal))
            FilterListedWorkspaces(result, boundWorkspace);
        return result;
    }

    private static void FilterListedWorkspaces(CallToolResult result, string boundWorkspace)
    {
        if (result.StructuredContent is JsonElement structured && structured.ValueKind is JsonValueKind.Array)
            result.StructuredContent = JsonSerializer.SerializeToElement(KeepBoundWorkspaces(structured, boundWorkspace));

        if (result.Content is null)
            return;

        result.Content = result.Content.Select(block => FilterListedWorkspaceText(block, boundWorkspace)).ToList();
    }

    private static ContentBlock FilterListedWorkspaceText(ContentBlock block, string boundWorkspace)
    {
        if (block is not TextContentBlock text || string.IsNullOrWhiteSpace(text.Text))
            return block;

        try
        {
            using var document = JsonDocument.Parse(text.Text);
            if (document.RootElement.ValueKind is not JsonValueKind.Array)
                return block;

            return new TextContentBlock { Text = JsonSerializer.Serialize(KeepBoundWorkspaces(document.RootElement, boundWorkspace)) };
        }
        catch (JsonException)
        {
            return block;
        }
    }

    private static JsonElement[] KeepBoundWorkspaces(JsonElement array, string boundWorkspace)
        => array.EnumerateArray()
            .Where(item => item.ValueKind is JsonValueKind.Object
                           && item.TryGetProperty("id", out var id)
                           && string.Equals(id.GetString(), boundWorkspace, StringComparison.Ordinal))
            .Select(item => item.Clone())
            .ToArray();

    private static CallToolResult Forbid(string? error)
        => new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = error ?? "Workspace binding refused this tool call." }]
        };
}
