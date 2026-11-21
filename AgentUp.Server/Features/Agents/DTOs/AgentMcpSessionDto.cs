namespace AgentUp.Server.Features.Agents.DTOs;

/// <summary>Who an agent's MCP session credential stands for: its workspace and that member's permissions.</summary>
public sealed record AgentMcpSessionDto(string WorkspaceId, IReadOnlyList<string> Permissions);
