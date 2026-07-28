namespace AgentUp.Server.Shared.Interfaces;

public sealed record McpToolResult(
    bool Succeeded,
    string Message,
    object? Data = null);
