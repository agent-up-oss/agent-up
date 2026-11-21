namespace AgentUp.Server.Features.Capabilities.Providers;

/// <summary>
/// The development tool root (<c>.agent-up-dev</c>) where a repository shell installs ACP
/// adapters that are not in nixpkgs, and the bin directories under it a capability launch adds
/// to its search path.
/// </summary>
public sealed class CapabilityRuntimePathProvider(string? devRoot)
{
    public string? DevRoot { get; } = Existing(devRoot);

    public IReadOnlyList<string> Directories()
    {
        if (DevRoot is null)
            return [];
        return new[]
        {
            Path.Join(DevRoot, "bin"),
            Path.Join(DevRoot, "npm", "node_modules", ".bin")
        }.Where(Directory.Exists).ToArray();
    }

    /// <summary>
    /// <c>AGENT_UP_DEV_ROOT</c> when it is set; otherwise the nearest <c>.agent-up-dev</c> above
    /// any of <paramref name="searchRoots"/>. A packaged Server has neither and uses its PATH alone.
    /// </summary>
    public static string? Resolve(string? configured, params string?[] searchRoots)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured.Trim());

        foreach (var root in searchRoots)
        {
            var directory = TryGetFullPath(root);
            while (!string.IsNullOrEmpty(directory))
            {
                var candidate = Path.Join(directory, ".agent-up-dev");
                if (Directory.Exists(candidate))
                    return candidate;
                var parent = Path.GetDirectoryName(directory);
                if (parent == directory)
                    break;
                directory = parent;
            }
        }

        return null;
    }

    private static string? Existing(string? directory)
        => !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) ? Path.GetFullPath(directory) : null;

    private static string? TryGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }
    }
}
