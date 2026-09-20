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

    public string ResolvePackageDirectory(string id, string version)
    {
        var encodedId = Encode(id);
        var encodedVersion = Encode(version);
        var path = Path.GetFullPath(Path.Join(RegistryRoot, "packages", encodedId, encodedVersion));
        if (!path.StartsWith(RegistryRoot, StringComparison.Ordinal))
            throw new InvalidOperationException("Capability package path escaped the registry root.");
        return path;
    }

    private static string Encode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Capability package id and version are required.");

        if (value.Any(character => character is '/' or '\\' or ':' or '\0'))
            throw new InvalidOperationException("Capability package id and version must not contain path separators.");

        return value;
    }
}
