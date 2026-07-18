namespace AgentUp.Server.Features.Mcp.DTOs;

public sealed record McpToolResult(
    bool Succeeded,
    string Message,
    object? Data = null);
