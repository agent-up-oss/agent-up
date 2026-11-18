namespace AgentUp.Server.Features.Capabilities.Interfaces;

public interface ICapabilityNixShellProvider
{
    /// <summary>
    /// The path of a <c>default.nix</c> that delivers <paramref name="packages"/> from the pinned
    /// nixpkgs commit, written under the Server's data directory for <paramref name="capabilityId"/>.
    /// </summary>
    string ShellPath(string capabilityId, IReadOnlyList<string> packages);
}
