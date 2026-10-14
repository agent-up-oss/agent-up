using AgentUp.Server.Features.Agents.Interfaces;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class RoleToolFilterProvider : IRoleToolFilter
{
    public void Restrict(McpServerOptions options, IReadOnlyList<string> allowlist)
    {
        if (allowlist.Count == 0)
            return;

        var allowed = allowlist.ToHashSet(StringComparer.Ordinal);
        var tools = options.ToolCollection;
        if (tools is not null)
        {
            foreach (var tool in tools.ToArray().Where(tool => !allowed.Contains(tool.ProtocolTool?.Name ?? "")))
                tools.Remove(tool);
        }

        options.Filters.Request.CallToolFilters.Add(next => (context, cancellationToken) =>
            RefuseAsync(next, context, allowed, cancellationToken));
    }

    private static async ValueTask<CallToolResult> RefuseAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        IReadOnlySet<string> allowed,
        CancellationToken cancellationToken)
    {
        var name = context.Params?.Name ?? "";
        if (allowed.Contains(name))
            return await next(context, cancellationToken);
        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = $"Tool '{name}' is outside this workspace role allowlist." }]
        };
    }
}
