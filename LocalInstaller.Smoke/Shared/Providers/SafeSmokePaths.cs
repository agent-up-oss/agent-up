namespace AgentUp.PackageSmoke.Shared.Providers;

public static class SafeSmokePaths
{
    public static string ExistingRoot(string path, string name)
    {
        var root = Root(path, name);
        if (!Directory.Exists(root))
            throw new InvalidOperationException($"{name} directory does not exist: {root}");

        return root;
    }

    public static string Root(string path, string name)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"{name} directory must not be empty.");

        return Path.GetFullPath(path);
    }

    public static string Identifier(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
            throw new InvalidOperationException($"{name} must not be empty.");

        if (value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
            throw new InvalidOperationException($"{name} contains unsafe characters.");

        if (value[0] is '-' or '.')
            throw new InvalidOperationException($"{name} must start with a letter or digit.");

        return value;
    }

    public static string Child(string root, params string[] parts)
    {
        var rootFullPath = Path.GetFullPath(root);
        var candidate = rootFullPath;
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part) || Path.IsPathRooted(part))
                throw new InvalidOperationException($"Invalid smoke path component: {part}");

            candidate = Path.Join(candidate, part);
        }

        candidate = Path.GetFullPath(candidate);
        if (!IsUnderRoot(rootFullPath, candidate))
            throw new InvalidOperationException($"Path escaped smoke root: {candidate}");

        return candidate;
    }

    public static string RequiredFile(string root, params string[] parts)
    {
        var path = Child(root, parts);
        if (!File.Exists(path))
            throw new InvalidOperationException($"Required file does not exist: {path}");

        return path;
    }

    private static bool IsUnderRoot(string root, string candidate)
        => string.Equals(root, candidate, StringComparison.Ordinal)
           || candidate.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                               + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
