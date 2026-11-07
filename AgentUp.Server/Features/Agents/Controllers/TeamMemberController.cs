using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Services;

namespace AgentUp.Server.Features.Agents.Controllers;

/// <summary>Slice boundary other slices use to ask what a workspace's team member may do.</summary>
public sealed class TeamMemberController(TeamMemberService members)
{
    public TeamMemberDto? Describe(string workspaceId) => members.Describe(workspaceId);

    public AgentActionResult Set(string workspaceId, TeamMemberKind kind) => members.Set(workspaceId, kind);

    /// <summary>Null when the workspace may raise review threads; otherwise why not.</summary>
    public string? ReviewRefusal(string workspaceId) => members.ReviewRefusal(workspaceId);

    public IReadOnlySet<string> OperationPermissions(string workspaceId) =>
        members.Grants(workspaceId).OperationPermissions;
}
