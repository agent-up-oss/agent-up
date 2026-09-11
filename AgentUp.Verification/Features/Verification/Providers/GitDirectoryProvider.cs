namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Resolves the Git directory for a checkout, following the pointer file that linked
/// worktrees use so each worktree keeps its own receipts.
/// </summary>
public sealed class GitDirectoryProvider
{
    private const string PointerPrefix = "gitdir:";

    /// <summary>
    /// Returns the absolute Git directory, or null when this is not a Git checkout.
    /// </summary>
    public string? Resolve(string repositoryRoot)
    {
        var candidate = Path.Join(repositoryRoot, ".git");

        if (Directory.Exists(candidate))
            return candidate;

        if (!File.Exists(candidate))
            return null;

        var pointer = File.ReadAllText(candidate).Trim();
        if (!pointer.StartsWith(PointerPrefix, StringComparison.Ordinal))
            return null;

        var target = pointer[PointerPrefix.Length..].Trim();
        return Path.IsPathRooted(target) ? target : Path.GetFullPath(Path.Join(repositoryRoot, target));
    }
}
