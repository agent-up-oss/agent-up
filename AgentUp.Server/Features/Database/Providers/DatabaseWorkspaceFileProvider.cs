namespace AgentUp.Server.Features.Database.Providers;

public static class DatabaseWorkspaceFileProvider
{
    public static IEnumerable<string> ReadEnvironmentFileLines(string worktreePath, string environmentFile)
    {
        var path = ResolveWorkspaceRootFile(worktreePath, environmentFile, "Environment file");
        if (!File.Exists(path))
            return [];

        return File.ReadLines(path);
    }

    private static string ResolveWorkspaceRootFile(string root, string fileName, string pathKind)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName != fileName.Trim())
            throw new InvalidOperationException($"{pathKind} paths must not be empty.");

        if (fileName.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"{pathKind} must stay under the workspace root.");

        if (Path.IsPathRooted(fileName) || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
            throw new InvalidOperationException($"{pathKind} must be a file name relative to the workspace root.");

        if (fileName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '_' and not '-'))
            throw new InvalidOperationException($"{pathKind} contains unsafe characters.");

        var rootFullPath = Path.GetFullPath(root);
        return Path.Join(rootFullPath, fileName);
    }
}
