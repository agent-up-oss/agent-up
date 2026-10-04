namespace AgentUp.Server.Features.Workspaces.DTOs;

/// <summary>
/// Which workspace a caller named, and the worktree path that follows from it.
/// </summary>
/// <remarks>
/// A remote MCP caller shares no filesystem with the Server, so it names a registered
/// workspace id; a caller on the Server host keeps naming an absolute worktree path. Both
/// arrive at the same place - a path the tool can work in - but a caller that supplies
/// both has stated two things that can disagree, and a caller that supplies neither has
/// stated nothing, so each is reported rather than guessed at.
/// </remarks>
public sealed record WorkspaceTargetResolution(
    WorkspaceTargetOutcome Outcome,
    string? WorkspaceId,
    string? WorktreePath,
    string? Error)
{
    public bool Resolved => Outcome is WorkspaceTargetOutcome.Resolved;

    public static WorkspaceTargetResolution FromPath(string worktreePath)
        => new(WorkspaceTargetOutcome.Resolved, null, worktreePath, null);

    public static WorkspaceTargetResolution FromRegisteredWorkspace(string workspaceId, string worktreePath)
        => new(WorkspaceTargetOutcome.Resolved, workspaceId, worktreePath, null);

    public static WorkspaceTargetResolution Neither()
        => new(
            WorkspaceTargetOutcome.Neither,
            null,
            null,
            "Pass exactly one of workspaceId or worktreePath. Both were omitted. "
            + "Use workspaceId for a registered workspace, or worktreePath for an absolute path on the Server host.");

    public static WorkspaceTargetResolution Both()
        => new(
            WorkspaceTargetOutcome.Both,
            null,
            null,
            "Pass exactly one of workspaceId or worktreePath, not both.");

    public static WorkspaceTargetResolution UnknownId(string workspaceId)
        => new(
            WorkspaceTargetOutcome.UnknownId,
            workspaceId,
            null,
            $"Workspace '{workspaceId}' is not registered. "
            + "Call list_workspaces for the registered workspace ids, or pass worktreePath instead.");
}
