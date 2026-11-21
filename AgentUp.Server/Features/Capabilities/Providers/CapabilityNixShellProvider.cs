using AgentUp.Capabilities.Common.Features.NixRuntime.Models;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Capabilities.Providers;

/// <summary>
/// Writes the pinned Nix shell a capability launches through. The expression is generated from
/// the capability's own package list, so the version a capability runs is decided by the code
/// compiled into this Server rather than by a file shipped beside it.
/// </summary>
public sealed class CapabilityNixShellProvider(string dataDirectory) : ICapabilityNixShellProvider
{
    private readonly object _gate = new();

    public string ShellPath(string capabilityId, IReadOnlyList<string> packages)
    {
        var root = Path.GetFullPath(Path.Join(dataDirectory, "capabilities", "nix"));
        var directory = Path.GetFullPath(Path.Join(root, SafeName(capabilityId)));
        if (!directory.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"Capability id '{capabilityId}' does not name a directory.", nameof(capabilityId));

        var path = Path.Join(directory, "default.nix");
        var expression = FirstPartyNixPin.DefaultNix(packages);
        lock (_gate)
        {
            if (!File.Exists(path) || !File.ReadAllText(path).Equals(expression, StringComparison.Ordinal))
            {
                Directory.CreateDirectory(directory);
                File.WriteAllText(path, expression);
            }
        }

        return path;
    }

    private static string SafeName(string id)
        => new(id.Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '_').ToArray());
}
