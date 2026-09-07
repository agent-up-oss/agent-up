namespace AgentUp.Server.Shared.Providers;

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

    public static string ResolveWorkspaceRootFile(string root, string relativeFilePath, string pathKind)
    {
        if (string.IsNullOrWhiteSpace(relativeFilePath) || relativeFilePath != relativeFilePath.Trim())
            throw new InvalidOperationException($"{pathKind} paths must not be empty.");

        if (Path.IsPathRooted(relativeFilePath))
            throw new InvalidOperationException($"{pathKind} must be relative to the workspace root.");

        if (relativeFilePath.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"{pathKind} must stay under the workspace root.");

        foreach (var segment in relativeFilePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment is "." or "..")
                throw new InvalidOperationException($"{pathKind} must stay under the workspace root.");

            if (segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
                throw new InvalidOperationException($"{pathKind} contains unsafe characters.");
        }

        return ResolveWorkspacePath(root, relativeFilePath, pathKind);
    }
}
