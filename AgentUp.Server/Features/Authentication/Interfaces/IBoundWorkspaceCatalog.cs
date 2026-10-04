namespace AgentUp.Server.Features.Authentication.Interfaces;

public interface IBoundWorkspaceCatalog
{
    Task<bool> PathTargetsWorkspaceAsync(
        string boundWorkspace,
        string path,
        CancellationToken cancellationToken);

    /// <summary>
    /// The worktree path of the bound workspace, or null when no workspace is registered
    /// under that id. Used to fill in a path parameter the caller was not allowed to see.
    /// </summary>
    string? WorktreePathFor(string boundWorkspace);
}
