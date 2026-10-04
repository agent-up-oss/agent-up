using AgentUp.Server.Features.Authentication.Interfaces;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Providers;

/// <summary>
/// Narrows a session to the operations the caller's token actually grants. Removing a tool from
/// the session collection is what keeps it out of tools/list; the call filter is what makes
/// knowing the name worthless, since a client may remember a tool the Server stopped advertising.
/// </summary>
public sealed class McpToolPermissionFilterProvider : IMcpToolPermissionFilterProvider
{
    public void Apply(McpServerOptions options, IReadOnlySet<string> grantedPermissions)
    {
        RemoveForbiddenTools(options, grantedPermissions);
        options.Filters.Request.CallToolFilters.Add(next => (context, cancellationToken) =>
            RefuseForbiddenCallAsync(next, context, grantedPermissions, cancellationToken));
    }

    /// <summary>
    /// Composes with the per-endpoint allowlist rather than replacing it: this only ever removes
    /// what the endpoint already decided to keep, so neither filter can widen the other.
    /// </summary>
    private static void RemoveForbiddenTools(McpServerOptions options, IReadOnlySet<string> grantedPermissions)
    {
        var tools = options.ToolCollection;
        if (tools is null)
            return;

        foreach (var tool in tools.ToArray().Where(tool => !IsAllowed(tool.ProtocolTool?.Name, grantedPermissions)))
        {
            tools.Remove(tool);
        }
    }

    private static async ValueTask<CallToolResult> RefuseForbiddenCallAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        IReadOnlySet<string> grantedPermissions,
        CancellationToken cancellationToken)
    {
        var name = context.Params?.Name;
        return IsAllowed(name, grantedPermissions)
            ? await next(context, cancellationToken)
            : Refuse(Refusal(name, grantedPermissions));
    }

    public static bool IsAllowed(string? tool, IReadOnlySet<string> grantedPermissions)
    {
        var required = tool is null ? null : McpToolPermissionMap.For(tool);
        return required is not null && grantedPermissions.Contains(required);
    }

    /// <summary>
    /// An undeclared tool fails closed. A scoped caller must never reach an operation whose cost
    /// nobody classified, and the architecture suite already refuses to let that state ship.
    /// </summary>
    public static string Refusal(string? tool, IReadOnlySet<string> grantedPermissions)
    {
        var name = string.IsNullOrWhiteSpace(tool) ? "(unnamed)" : tool;
        var required = tool is null ? null : McpToolPermissionMap.For(tool);
        if (required is null)
            return $"Tool '{name}' declares no operation permission, so a permission-scoped token cannot call it.";

        return $"Missing permission '{required}' for tool '{name}'. This token grants: "
               + $"{Describe(grantedPermissions)}.";
    }

    private static string Describe(IReadOnlySet<string> grantedPermissions)
        => grantedPermissions.Count == 0
            ? "no operation permissions"
            : string.Join(", ", grantedPermissions.Order(StringComparer.Ordinal));

    private static CallToolResult Refuse(string error)
        => new()
        {
            IsError = true,
            Content = [new TextContentBlock { Text = error }]
        };
}
