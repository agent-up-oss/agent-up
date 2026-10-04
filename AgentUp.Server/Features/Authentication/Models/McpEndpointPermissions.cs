using AgentUp.Server.Features.Authentication.DTOs;

namespace AgentUp.Server.Features.Authentication.Models;

/// <summary>
/// The permission floor for each named MCP server. A remote caller that cannot satisfy the
/// floor cannot reach the endpoint at all; individual tools may still require more.
/// </summary>
public static class McpEndpointPermissions
{
    private const string PolicyPrefix = "mcp-endpoint:";

    public static IReadOnlyList<McpEndpointPermission> All { get; } =
    [
        new("/mcp/commits", OperationPermissions.GitWrite),
        new("/mcp/verification", OperationPermissions.GitWrite),
        new("/mcp/orchestration", OperationPermissions.WorkspaceRead),
        new("/mcp/browser", OperationPermissions.BrowserControl),
        new("/mcp/audit", OperationPermissions.DiagnosticsRead),
        new("/mcp/capabilities", OperationPermissions.ServerRead)
    ];

    public static IReadOnlyList<string> Permissions { get; } = All
        .Select(endpoint => endpoint.Permission)
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    public static string PolicyName(string permission) => PolicyPrefix + permission;
}
