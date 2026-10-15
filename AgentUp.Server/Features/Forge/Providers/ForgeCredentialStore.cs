using AgentUp.Server.Features.Forge.Interfaces;

namespace AgentUp.Server.Features.Forge.Providers;

public sealed class ForgeCredentialStore : IForgeCredentialStore
{
    private readonly string _root;

    public ForgeCredentialStore(string dataDirectory)
    {
        _root = Path.GetFullPath(Path.Join(dataDirectory, "forge-credentials"));
    }

    public string? Read(string workspaceId, string moduleId)
    {
        var path = PathFor(workspaceId, moduleId);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }

    public void Write(string workspaceId, string moduleId, string token)
    {
        var path = PathFor(workspaceId, moduleId);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, token);
    }

    public void Delete(string workspaceId, string moduleId)
    {
        var path = PathFor(workspaceId, moduleId);
        if (File.Exists(path))
            File.Delete(path);
    }

    private string PathFor(string workspaceId, string moduleId)
    {
        var workspace = RequireSegment(workspaceId, nameof(workspaceId));
        var module = RequireSegment(moduleId, nameof(moduleId));
        var path = Path.GetFullPath(Path.Join(_root, workspace, module));
        if (!IsUnderRoot(_root, path))
            throw new InvalidOperationException("Forge credential path escaped the store root.");
        return path;
    }

    /// <summary>
    /// Workspace and module ids become directory names. Reject traversal and separators before
    /// any filesystem call so a caller cannot point the store outside its root. The value itself
    /// is returned only after those checks, matching the CodeQL single-segment sanitizer.
    /// </summary>
    internal static string RequireSegment(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains("..", StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value is "."
            || Path.GetFileName(value) != value
            || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException($"Forge credential {name} is not a safe path segment.");
        }

        return value;
    }

    internal static bool IsUnderRoot(string root, string path)
    {
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal);
    }
}
