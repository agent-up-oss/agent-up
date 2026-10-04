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

        if (PathsEqual(path, workspace.WorktreePath) || PathsEqual(path, workspace.RepositoryPath))
            return true;

        var byPath = FindByPath(path);
        if (byPath is not null)
            return string.Equals(byPath.Id, boundWorkspace, StringComparison.Ordinal);

        var queuePath = await QueueWorktreePathAsync(workspace, cancellationToken);
        return queuePath is not null && PathsEqual(path, queuePath);
    }

    private Workspace? FindByPath(string path)
        => workspaces.GetAll().FirstOrDefault(candidate =>
            PathsEqual(path, candidate.WorktreePath) || PathsEqual(path, candidate.RepositoryPath));

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

    private static bool PathsEqual(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!Path.IsPathRooted(left) || !Path.IsPathRooted(right))
            return false;

        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException
            or NotSupportedException
            or PathTooLongException)
        {
            return false;
        }
    }
}
