namespace AgentUp.Server.Features.Capabilities.Providers;

public sealed class CapabilityRuntimePathProvider
{
    public IReadOnlyList<string> Directories(string? registryRoot, IEnumerable<string> packageDirectories)
    {
        var packageBins = packageDirectories
            .Select(directory => Path.Join(directory, "bin"))
            .Where(Directory.Exists);
        var parent = Parent(registryRoot);
        if (parent is null)
            return packageBins.ToArray();

        var siblingBins = new[]
        {
            Path.Join(parent, "bin"),
            Path.Join(parent, "npm", "node_modules", ".bin")
        }.Where(Directory.Exists);

        return packageBins.Concat(siblingBins).ToArray();
    }

    public string? DevRoot(string? registryRoot)
    {
        var parent = Parent(registryRoot);
        if (parent is null)
            return null;

        return Directory.Exists(Path.Join(parent, "bin"))
               || Directory.Exists(Path.Join(parent, "npm", "node_modules", ".bin"))
            ? parent
            : null;
    }

    private static string? Parent(string? registryRoot)
    {
        if (string.IsNullOrWhiteSpace(registryRoot))
            return null;

        var parent = Path.GetDirectoryName(Path.GetFullPath(registryRoot));
        return string.IsNullOrWhiteSpace(parent) ? null : parent;
    }
}
