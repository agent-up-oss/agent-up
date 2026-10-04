using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Workspaces.Controllers;

/// <summary>
/// The Workspaces boundary sibling slices use to turn a workspace id or a worktree path
/// into the one worktree path their tools operate on.
/// </summary>
public sealed class WorkspaceTargetController(WorkspaceTargetService targets)
{
    public WorkspaceTargetResolution Resolve(string? workspaceId, string? worktreePath)
        => targets.Resolve(workspaceId, worktreePath);
}
