using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Capabilities.Common.Features.CapabilityDistribution.Providers;

public sealed class CapabilityToolCacheLayout
{
    private readonly string _root;

    public CapabilityToolCacheLayout(string root)
    {
        _root = Path.GetFullPath(root);
    }

    public string GetDownloadPath(CapabilityArtifact artifact) =>
        ValidateUnderRoot(Path.Join(_root, "downloads", SafeSegment(artifact.CapabilityId), SafeSegment(artifact.Version), "artifact.zip"));

    public string GetInstallDirectory(CapabilityArtifact artifact) =>
        ValidateUnderRoot(Path.Join(_root, "capabilities", SafeSegment(artifact.CapabilityId), SafeSegment(artifact.Version)));

    public string GetRegistrationPath(CapabilityArtifact artifact) =>
        ValidateUnderRoot(Path.Join(_root, "registry", SafeSegment(artifact.CapabilityId), SafeSegment(artifact.Version) + ".json"));

    private string ValidateUnderRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var rootWithSeparator = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) && fullPath != _root)
            throw new InvalidOperationException("Capability path escapes the Agent-Up tool cache.");

        return fullPath;
    }

    private static string SafeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException("Capability path segment is required.");

        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"Capability path segment '{value}' is invalid.");

        return value;
    }
}
