using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Services;

/// <summary>
/// Turns the workspace an MCP or REST caller named into the worktree path the owning slice
/// works in.
/// </summary>
public sealed class WorkspaceTargetService(WorkspaceRegistry registry)
{
    public WorkspaceTargetResolution Resolve(string? workspaceId, string? worktreePath)
    {
        var hasId = !string.IsNullOrWhiteSpace(workspaceId);
        var hasPath = !string.IsNullOrWhiteSpace(worktreePath);

        if (hasId && hasPath)
            return WorkspaceTargetResolution.Both();
        if (!hasId && !hasPath)
            return WorkspaceTargetResolution.Neither();
        if (hasPath)
            return WorkspaceTargetResolution.FromPath(worktreePath!.Trim());

        var trimmedId = workspaceId!.Trim();
        var workspace = registry.GetById(trimmedId);
        return workspace is null
            ? WorkspaceTargetResolution.UnknownId(trimmedId)
            : WorkspaceTargetResolution.FromRegisteredWorkspace(workspace.Id, workspace.WorktreePath);
    }
}
