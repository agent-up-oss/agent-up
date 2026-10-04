using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class BoundWorkspaceCatalog(
    WorkspaceQueryController workspaces,
    CommitsController commits) : IBoundWorkspaceCatalog
{
    public async Task<bool> PathTargetsWorkspaceAsync(
        string boundWorkspace,
        string path,
        CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(boundWorkspace);
        if (workspace is null)
            return false;

        if (OwnsPath(workspace, path))
            return true;

        var byPath = FindByPath(path);
        if (byPath is not null)
            return string.Equals(byPath.Id, boundWorkspace, StringComparison.Ordinal);

        var queuePath = await QueueWorktreePathAsync(workspace, cancellationToken);
        return queuePath is not null && PathsEqual(path, queuePath);
    }

    public string? WorktreePathFor(string boundWorkspace)
        => workspaces.GetById(boundWorkspace)?.WorktreePath;

    private Workspace? FindByPath(string path)
        => workspaces.GetAll().FirstOrDefault(candidate => OwnsPath(candidate, path));

    public static bool OwnsPath(Workspace workspace, string path)
        => PathsEqual(path, workspace.WorktreePath) || PathsEqual(path, workspace.RepositoryPath);

    private async Task<string?> QueueWorktreePathAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        try
        {
            var status = await commits.GetStatusAsync(workspace.WorktreePath, cancellationToken);
            return status.QueueWorktreePath;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or IOException
            or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool PathsEqual(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!BothRooted(left, right))
            return false;

        return TryGetFullPath(left, Path.GetFullPath, out var fullLeft)
               && TryGetFullPath(right, Path.GetFullPath, out var fullRight)
               && string.Equals(fullLeft, fullRight, StringComparison.OrdinalIgnoreCase);
    }

    public static bool BothRooted(string left, string right)
        => Path.IsPathRooted(left) && Path.IsPathRooted(right);

    public static bool TryGetFullPath(string path, Func<string, string> resolve, out string? fullPath)
    {
        try
        {
            fullPath = resolve(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            fullPath = null;
            return false;
        }
    }
}
