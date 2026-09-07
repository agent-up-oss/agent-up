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
        EnsureLexicallyContained(rootFullPath, fullPath, pathKind);
        EnsurePhysicallyContained(rootFullPath, fullPath, pathKind);

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

    private static void EnsureLexicallyContained(string rootFullPath, string fullPath, string pathKind)
    {
        var relative = Path.GetRelativePath(rootFullPath, fullPath);
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal))
            throw new InvalidOperationException($"{pathKind} must stay under the workspace root.");
    }

    private static void EnsurePhysicallyContained(string rootFullPath, string fullPath, string pathKind)
    {
        var physicalRoot = ResolvePhysicalPath(rootFullPath);
        var physicalPath = ResolvePhysicalPath(fullPath);
        var relative = Path.GetRelativePath(physicalRoot, physicalPath);
        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) || relative.StartsWith("..\\", StringComparison.Ordinal))
            throw new InvalidOperationException($"{pathKind} must stay under the workspace root.");
    }

    private static string ResolvePhysicalPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Path.IsPathRooted(fullPath))
            return fullPath;

        var root = Path.GetPathRoot(fullPath)!;
        var segments = fullPath[root.Length..]
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);

        var current = root;
        foreach (var segment in segments)
        {
            current = Path.Join(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                continue;

            var linkTarget = File.ResolveLinkTarget(current, returnFinalTarget: true)
                ?? Directory.ResolveLinkTarget(current, returnFinalTarget: true);
            if (linkTarget is null)
                continue;

            current = Path.IsPathRooted(linkTarget.FullName)
                ? Path.GetFullPath(linkTarget.FullName)
                : Path.GetFullPath(Path.Join(Path.GetDirectoryName(current)!, linkTarget.FullName));
        }

        return Path.GetFullPath(current);
    }
}
