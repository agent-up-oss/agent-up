namespace AgentUp.Server.Features.Workspaces.DTOs;

/// <summary>How a caller's attempt to name a workspace turned out.</summary>
public enum WorkspaceTargetOutcome
{
    /// <summary>Exactly one target was supplied and it named a usable worktree path.</summary>
    Resolved,

    /// <summary>Neither a workspace id nor a worktree path was supplied.</summary>
    Neither,

    /// <summary>Both a workspace id and a worktree path were supplied.</summary>
    Both,

    /// <summary>A workspace id was supplied but no workspace is registered under it.</summary>
    UnknownId
}
