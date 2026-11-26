namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// The Unix socket a sandboxed agent uses to reach the Server after its network namespace is
/// unshared. The path stays under the data directory and is unique per process so parallel
/// Servers do not collide.
/// </summary>
public sealed class AgentLoopbackEndpoint
{
    public AgentLoopbackEndpoint(string dataDirectory)
    {
        var root = Path.GetFullPath(dataDirectory);
        var directory = Path.Join(root, "agent-loopback");
        Directory.CreateDirectory(directory);
        SocketPath = Path.Join(directory, $"{Guid.NewGuid():N}.sock");
        if (!UnderRoot(root, SocketPath))
            throw new InvalidOperationException("The agent loopback socket must stay under the Server data directory.");
    }

    public string SocketPath { get; }

    private static bool UnderRoot(string root, string path)
    {
        var relative = System.IO.Path.GetRelativePath(root, path);
        return relative != ".."
               && !relative.StartsWith($"..{System.IO.Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !relative.StartsWith($"..{System.IO.Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
