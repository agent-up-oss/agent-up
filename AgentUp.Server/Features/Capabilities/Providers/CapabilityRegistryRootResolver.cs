namespace AgentUp.Server.Features.Capabilities.Providers;

public static class CapabilityRegistryRootResolver
{
    public static string Resolve(string? configured, string dataDirectory, params string?[] searchRoots)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured.Trim());

        foreach (var root in searchRoots)
        {
            var found = WalkForPackedRegistry(root);
            if (found is not null)
                return found;
        }

        return Path.GetFullPath(Path.Join(dataDirectory, "capability-registry"));
    }

    private static string? WalkForPackedRegistry(string? start)
    {
        var directory = TryGetFullPath(start);
        if (directory is null)
            return null;

        if (File.Exists(directory))
            directory = Path.GetDirectoryName(directory);

        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Join(directory, ".agent-up-dev", "capability-registry");
            if (File.Exists(Path.Join(candidate, "index.json")))
                return Path.GetFullPath(candidate);

            var parent = Path.GetDirectoryName(directory);
            if (parent == directory)
                break;
            directory = parent;
        }

        return null;
    }

    private static string? TryGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }
    }
}
