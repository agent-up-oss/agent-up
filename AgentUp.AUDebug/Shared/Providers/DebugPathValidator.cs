using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Shared.Providers;

public sealed class DebugPathValidator : IDebugPathValidator
{
    public DebugPathValidator(string repositoryRoot)
    {
        RepositoryRoot = Path.GetFullPath(repositoryRoot);
        SessionDirectory = JoinUnderRoot(".git", "agent-up", "au-debug");
        LogsDirectory = Path.Join(SessionDirectory, "logs");
        ScreenshotsDirectory = Path.Join(SessionDirectory, "screenshots");
    }

    public string RepositoryRoot { get; }
    public string SessionDirectory { get; }
    public string LogsDirectory { get; }
    public string ScreenshotsDirectory { get; }

    public string JoinUnderRoot(params string[] segments)
    {
        var path = RepositoryRoot;
        foreach (var segment in segments)
            path = Path.Join(path, segment);
        return EnsureUnderRoot(path);
    }

    public string EnsureUnderRoot(string path)
    {
        var full = Path.GetFullPath(path);
        var root = RepositoryRoot.EndsWith(Path.DirectorySeparatorChar)
            ? RepositoryRoot
            : RepositoryRoot + Path.DirectorySeparatorChar;
        if (full.Equals(RepositoryRoot, StringComparison.Ordinal))
            return full;
        if (!full.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidOperationException($"Path '{full}' is outside the repository root.");
        return full;
    }
}
