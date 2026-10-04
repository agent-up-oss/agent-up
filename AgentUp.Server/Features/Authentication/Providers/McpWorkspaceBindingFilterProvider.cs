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
        return HideWorkspaceParameters(await next(context, cancellationToken));
    }

    public ListToolsResult HideWorkspaceParameters(ListToolsResult result)
    {
        if (result.Tools is null)
            return result;

        result.Tools = result.Tools.Select(StripWorkspaceParameters).ToList();
        return result;
    }

    private Tool StripWorkspaceParameters(Tool tool)
        => new()
        {
            Name = tool.Name,
            Title = tool.Title,
            Description = tool.Description,
            InputSchema = schemas.StripWorkspaceTargetProperties(tool.InputSchema),
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
        var arguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        CopyArguments(context.Params?.Arguments, arguments);
        var decision = await binder.BindAsync(
            boundWorkspace,
            arguments,
            schemas.WorkspaceTargetPropertyNames(ToolInputSchema(context.MatchedPrimitive)),
            cancellationToken);
        if (!decision.Allowed)
            return Forbid(decision.Error!);

        WriteBoundArguments(context.Params, decision.Arguments);

        var result = await next(context, cancellationToken);
        if (string.Equals(context.Params?.Name, "list_workspaces", StringComparison.Ordinal))
            FilterListedWorkspaces(result, boundWorkspace);
        return result;
    }

    public static JsonElement ToolInputSchema(object? matchedPrimitive)
        => matchedPrimitive is McpServerTool tool ? tool.ProtocolTool.InputSchema : default;

    public static void CopyArguments(
        IEnumerable<KeyValuePair<string, JsonElement>>? supplied,
        IDictionary<string, JsonElement> destination)
    {
        if (supplied is null)
            return;

        foreach (var (key, value) in supplied)
            destination[key] = value;
    }

    public static void WriteBoundArguments(
        CallToolRequestParams? parameters,
        IReadOnlyDictionary<string, JsonElement> arguments)
    {
        if (parameters is null)
            return;

        parameters.Arguments = new Dictionary<string, JsonElement>(arguments, StringComparer.OrdinalIgnoreCase);
    }

    public static void FilterListedWorkspaces(CallToolResult result, string boundWorkspace)
    {
        if (result.StructuredContent is JsonElement structured && structured.ValueKind is JsonValueKind.Array)
            result.StructuredContent = JsonSerializer.SerializeToElement(KeepBoundWorkspaces(structured, boundWorkspace));

        if (result.Content is null)
            return;

        result.Content = result.Content.Select(block => FilterListedWorkspaceText(block, boundWorkspace)).ToList();
    }

    private static ContentBlock FilterListedWorkspaceText(ContentBlock block, string boundWorkspace)
    {
        if (block is not TextContentBlock text)
            return block;
        if (string.IsNullOrWhiteSpace(text.Text))
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

    public static bool IsBoundWorkspace(JsonElement item, string boundWorkspace)
    {
        if (item.ValueKind is not JsonValueKind.Object)
            return false;
        if (!item.TryGetProperty("id", out var id))
            return false;
        return string.Equals(id.GetString(), boundWorkspace, StringComparison.Ordinal);
    }

    private static JsonElement[] KeepBoundWorkspaces(JsonElement array, string boundWorkspace)
        => array.EnumerateArray()
            .Where(item => IsBoundWorkspace(item, boundWorkspace))
            .Select(item => item.Clone())
            .ToArray();

    private static CallToolResult Forbid(string error)
        => new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = error }]
        };
}
