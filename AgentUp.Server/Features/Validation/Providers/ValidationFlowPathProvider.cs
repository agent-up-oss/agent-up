using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Validation.Providers;

public sealed class ValidationFlowPathProvider(WorkspaceQueryController workspaces)
{
    public const string RelativeFlowFilePath = ".agent-up/validation-flows.json";

    public string? GetFlowFilePath(string workspaceId)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var worktreeRoot = Path.GetFullPath(workspace.WorktreePath);
        if (!Directory.Exists(worktreeRoot))
            return null;

        var flowFile = Path.GetFullPath(Path.Join(worktreeRoot, RelativeFlowFilePath));
        return IsUnderRoot(worktreeRoot, flowFile) ? flowFile : null;
    }

    internal static bool IsUnderRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
               && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
