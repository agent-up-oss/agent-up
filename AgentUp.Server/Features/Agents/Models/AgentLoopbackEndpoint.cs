using System.Text;

namespace AgentUp.Server.Features.Agents.Models;

/// <summary>
/// The Unix socket a sandboxed agent uses to reach the Server after its network namespace is
/// unshared. The path stays short enough for Linux <c>sockaddr_un</c>, unique per process so
/// parallel Servers do not collide, and owner-only once Kestrel creates the file.
/// </summary>
public sealed class AgentLoopbackEndpoint
{
    /// <summary>Linux <c>sun_path</c> bytes, excluding the terminating NUL.</summary>
    public const int UnixPathMaxBytes = 107;

    public AgentLoopbackEndpoint(string dataDirectory)
    {
        var root = Path.GetFullPath(dataDirectory);
        var directory = Path.Join(root, "al");
        Directory.CreateDirectory(directory);
        var candidate = Path.Join(directory, $"{Environment.ProcessId}.sock");
        SocketPath = Fits(candidate) ? candidate : RuntimeSocketPath();
        if (Fits(candidate) && !UnderRoot(root, SocketPath))
            throw new InvalidOperationException("The agent loopback socket must stay under the Server data directory.");
        if (!Fits(SocketPath))
            throw new InvalidOperationException("The agent loopback socket path exceeds the Unix-domain length limit.");
    }

    public string SocketPath { get; }

    /// <summary>
    /// Adds the Unix listener next to the configured TCP URLs. Calling Kestrel
    /// <c>ListenUnixSocket</c> would replace those URLs.
    /// </summary>
    public static IReadOnlyList<string> CombineHostingUrls(string? configuredUrls, string socketPath)
    {
        var unix = $"http://unix:{socketPath}";
        var parts = (configuredUrls ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => !part.Contains("://unix:", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (parts.Count == 0)
            parts.Add("http://127.0.0.1:5000");
        parts.Add(unix);
        return parts;
    }

    public static bool IsTcpHttp(string address) =>
        address.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        && !address.Contains("://unix:", StringComparison.OrdinalIgnoreCase);

    public static string? TcpHttpUrl(IEnumerable<string> urls) =>
        urls.FirstOrDefault(IsTcpHttp);

    private static string RuntimeSocketPath()
    {
        var directory = Path.Join(Path.GetTempPath(), "al");
        Directory.CreateDirectory(directory);
        return Path.Join(directory, $"{Environment.ProcessId}.sock");
    }

    private static bool Fits(string path) => Encoding.UTF8.GetByteCount(path) <= UnixPathMaxBytes;

    private static bool UnderRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
               && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
