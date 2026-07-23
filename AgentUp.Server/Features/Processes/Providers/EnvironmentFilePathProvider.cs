namespace AgentUp.Server.Features.Processes.Providers;

public static class EnvironmentFilePathProvider
{
    public static string ResolveExistingWorkspaceFile(string worktreePath, string environmentFile)
    {
        var fullPath = ResolveWorkspaceFile(worktreePath, environmentFile);
        var safeFullPath = Path.GetFullPath(fullPath);
        if (!File.Exists(safeFullPath))
            throw new InvalidOperationException($"Environment file '{environmentFile}' was not found.");

        return safeFullPath;
    }

    private static string ResolveWorkspaceFile(string worktreePath, string environmentFile)
    {
        if (string.IsNullOrWhiteSpace(environmentFile))
            throw new InvalidOperationException("Environment file paths must not be empty.");

        if (Path.IsPathRooted(environmentFile))
            throw new InvalidOperationException($"Environment file '{environmentFile}' must be relative to the workspace root.");

        var root = Path.GetFullPath(worktreePath);
        var fullPath = Path.GetFullPath(Path.Join(root, environmentFile));
        var relative = Path.GetRelativePath(root, fullPath);
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal))
            throw new InvalidOperationException($"Environment file '{environmentFile}' must stay under the workspace root.");

        return fullPath;
    }
}
