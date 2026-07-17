namespace AgentUp.Packaging.Shared.Providers;

public static class PackagePathValidator
{
    private static readonly char[] PathSeparators = ['/', '\\'];

    public static string RequireFullyQualifiedPath(string path, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", parameterName);

        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException("Path must be fully qualified.", parameterName);

        return Path.GetFullPath(path);
    }

    public static string GetParentDirectory(string path, string parameterName)
    {
        var fullPath = RequireFullyQualifiedPath(path, parameterName);
        return Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Path must have a parent directory.", parameterName);
    }

    public static string CombineValidated(string root, string relativePath, string parameterName)
        => ResolveRelativeUnderRoot(root, relativePath, parameterName);

    public static string ResolveRelativeUnderRoot(string root, string relativePath, string parameterName)
    {
        var fullRoot = RequireFullyQualifiedPath(root, nameof(root));
        RequireSafeRelativePath(relativePath, parameterName);

        var fullPath = Path.GetFullPath(Path.Join(fullRoot, relativePath));
        if (!IsContainedIn(fullRoot, fullPath))
            throw new ArgumentException("Path must stay under the repository root.", parameterName);

        return fullPath;
    }

    public static string RequireChildPath(string parent, string childName, string parameterName)
    {
        var fullParent = RequireFullyQualifiedPath(parent, nameof(parent));
        var safeChildName = RequireSafePathComponent(childName, parameterName);
        return Path.Join(fullParent, safeChildName);
    }

    public static string ResolveRootOrRelativeUnderRoot(string root, string path, string parameterName)
    {
        if (Path.IsPathFullyQualified(path))
            return RequireFullyQualifiedPath(path, parameterName);

        return ResolveRelativeUnderRoot(root, path, parameterName);
    }

    public static string RequireSafePathComponent(string component, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(component))
            throw new ArgumentException("Path component must not be empty.", parameterName);

        if (component is "." or ".." || component.IndexOfAny(PathSeparators) >= 0)
            throw new ArgumentException("Path component must not contain path traversal.", parameterName);

        if (component.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '_' or '-')))
            throw new ArgumentException("Path component contains unsupported characters.", parameterName);

        return component;
    }

    public static void RequireSafeRelativePath(string relativePath, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Path must not be empty.", parameterName);

        if (Path.IsPathFullyQualified(relativePath))
            throw new ArgumentException("Path must be relative.", parameterName);

        foreach (var component in relativePath.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries))
            RequireSafePathComponent(component, parameterName);
    }

    public static bool IsContainedIn(string root, string candidate)
    {
        var fullRoot = TrimTrailingSeparators(Path.GetFullPath(root));
        var fullCandidate = TrimTrailingSeparators(Path.GetFullPath(candidate));
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        return string.Equals(fullRoot, fullCandidate, comparison)
            || fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, comparison)
            || fullCandidate.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, comparison);
    }

    private static string TrimTrailingSeparators(string path)
    {
        var root = Path.GetPathRoot(path);
        return string.Equals(path, root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            ? path
            : path.TrimEnd(PathSeparators);
    }
}
