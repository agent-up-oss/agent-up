namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentCliHomeProvider
{
    public AgentCliHomeProvider(string dataDirectory)
    {
        var root = Path.GetFullPath(dataDirectory);
        var home = Path.GetFullPath(Path.Join(root, "agent-cli-home"));
        if (!IsUnderRoot(root, home))
            throw new InvalidOperationException("The agent CLI home must stay under the Server data directory.");
        HomePath = home;
    }

    public string HomePath { get; }

    public void Ensure() => Directory.CreateDirectory(HomePath);

    internal static bool IsUnderRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
               && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
