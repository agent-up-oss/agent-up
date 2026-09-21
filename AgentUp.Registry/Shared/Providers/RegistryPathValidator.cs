using AgentUp.Registry.Shared.Interfaces;

namespace AgentUp.Registry.Shared.Providers;

public sealed class RegistryPathValidator : IRegistryPathValidator
{
    public RegistryPathValidator(string registryRoot)
    {
        if (string.IsNullOrWhiteSpace(registryRoot))
            throw new InvalidOperationException("Capability registry path is required.");

        RegistryRoot = Path.GetFullPath(registryRoot);
    }

    public string RegistryRoot { get; }

    public string ResolveIndexPath() => Path.Join(RegistryRoot, "index.json");

    public string ResolvePackageDirectory(string id, string version) => Resolve("packages", id, version);

    /// <summary>
    /// Where a pushed or downloaded archive is unpacked before it is installed. It belongs to the
    /// registry root, not to wherever the process happens to be running: a staging directory
    /// derived from the content root is the working copy when the Registry runs from its own
    /// project directory.
    /// </summary>
    public string StagingRoot => Path.Join(RegistryRoot, "staging");

    /// <summary>
    /// The canonical form of a path the registry is about to read from or write to, rejected
    /// when it resolves outside the registry root.
    /// </summary>
    /// <remarks>
    /// Package directories and staging roots are registry storage. Checking containment where
    /// the path is used, and not only where it was built, keeps a caller that assembles one
    /// itself from reaching the filesystem with it.
    /// </remarks>
    public string RequireWithinRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Capability registry path is required.");

        var full = Path.GetFullPath(path);
        var root = RegistryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException("Capability package path escaped the registry root.");

        return full;
    }

    private string Resolve(string area, string id, string version)
    {
        var path = Path.GetFullPath(Path.Join(RegistryRoot, area, Segment(id), Segment(version)));
        var areaRoot = Path.Join(RegistryRoot, area) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(areaRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("Capability package path escaped the registry root.");
        return path;
    }

    /// <summary>
    /// A package id or version is one path segment and nothing else.
    /// </summary>
    /// <remarks>
    /// These arrive from an HTTP route, so they are rejected rather than sanitised: silently
    /// rewriting a traversal attempt would serve or overwrite a package the caller did not name.
    /// </remarks>
    public static string Segment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Capability package id and version are required.");

        if (value is "." or ".." || value.Any(character => character is '/' or '\\' or ':' or '\0')
            || Path.GetFileName(value) != value)
        {
            throw new InvalidOperationException(
                "Capability package id and version must be a single path segment.");
        }

        return value;
    }
}
