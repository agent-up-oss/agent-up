namespace AgentUp.Server.Features.Processes.Providers;

public static class WorkspacePathProvider
{
    public static string ResolveWorkspacePath(string root, string? relativePath, string pathKind)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new InvalidOperationException("Workspace path must not be empty.");

        var rootFullPath = Path.GetFullPath(root);
        if (string.IsNullOrWhiteSpace(relativePath))
            return rootFullPath;

        if (Path.IsPathRooted(relativePath))
            throw new InvalidOperationException($"{pathKind} must be relative to the workspace root.");

        var fullPath = Path.GetFullPath(Path.Join(rootFullPath, relativePath));
        var relative = Path.GetRelativePath(rootFullPath, fullPath);
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal))
            throw new InvalidOperationException($"{pathKind} must stay under the workspace root.");

        return fullPath;
    }

    public static string ResolveWorkspaceRootFile(string root, string fileName, string pathKind)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName != fileName.Trim())
            throw new InvalidOperationException($"{pathKind} paths must not be empty.");

        if (fileName.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"{pathKind} must stay under the workspace root.");

        if (Path.IsPathRooted(fileName) || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            throw new InvalidOperationException($"{pathKind} must be a file name relative to the workspace root.");

        if (fileName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
            throw new InvalidOperationException($"{pathKind} contains unsafe characters.");

        var rootFullPath = ResolveWorkspacePath(root, null, pathKind);
        return Path.Join(rootFullPath, fileName);
    }
}
