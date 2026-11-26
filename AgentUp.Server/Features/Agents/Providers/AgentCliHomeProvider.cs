using System.Security.Cryptography;
using System.Text;

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

    /// <summary>
    /// A HOME directory for one workspace's ACP process and its subscription login. Session
    /// transcripts and credentials stay here so one workspace cannot read another.
    /// </summary>
    public string WorkspaceHome(string worktree)
    {
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(worktree))))[..16]
            .ToLowerInvariant();
        var home = Path.GetFullPath(Path.Join(HomePath, "workspaces", id));
        if (!IsUnderRoot(HomePath, home))
            throw new InvalidOperationException("A workspace agent home must stay under the agent CLI home.");
        return home;
    }

    public string EnsureWorkspaceHome(string worktree)
    {
        var home = WorkspaceHome(worktree);
        Directory.CreateDirectory(home);
        var token = Path.Join(HomePath, ".claude-oauth-token");
        var copy = Path.Join(home, ".claude-oauth-token");
        if (File.Exists(token) && !File.Exists(copy))
            File.Copy(token, copy);
        return home;
    }

    internal static bool IsUnderRoot(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
               && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !relative.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
