using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Reads HEAD from the checkout's Git directory. Linked worktrees follow the pointer file
/// the same way receipts do.
/// </summary>
public sealed class GitRevisionProvider(GitDirectoryProvider gitDirectories) : IGitRevisionProvider
{
    public string Resolve(string repositoryRoot)
    {
        var gitDirectory = gitDirectories.Resolve(repositoryRoot);
        if (gitDirectory is null)
            return "unknown";

        var headPath = Path.Join(gitDirectory, "HEAD");
        if (!File.Exists(headPath))
            return "unknown";

        var head = File.ReadAllText(headPath).Trim();
        if (head.StartsWith("ref:", StringComparison.Ordinal))
        {
            var refPath = Path.Join(gitDirectory, head["ref:".Length..].Trim());
            return File.Exists(refPath) ? File.ReadAllText(refPath).Trim() : "unknown";
        }

        return head.Length == 0 ? "unknown" : head;
    }
}
