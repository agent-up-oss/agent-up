namespace AgentUp.Server.Features.Authentication.Models;

/// <summary>
/// One MCP endpoint path and the operation permission a remote caller needs to reach it.
/// </summary>
public sealed record McpEndpointPermission(string Path, string Permission);
