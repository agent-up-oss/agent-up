using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Services;

namespace AgentUp.Server.Features.Agents.Controllers;

/// <summary>Resolves the credential an agent presents on an Agent-Up MCP server.</summary>
public sealed class AgentMcpSessionController(AgentMcpSessionService sessions, TeamMemberService members)
{
    /// <summary>
    /// The workspace a live credential belongs to and its team member's operation permissions,
    /// read now rather than at issue so a changed member kind applies to the next call.
    /// </summary>
    public AgentMcpSessionDto? Resolve(string token) =>
        sessions.WorkspaceFor(token) is { } workspaceId
            ? new AgentMcpSessionDto(workspaceId, members.Grants(workspaceId).OperationPermissions.Order(StringComparer.Ordinal).ToArray())
            : null;
}
