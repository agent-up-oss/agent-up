namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public static class RepositoryPaths
{
    public static string FindRepositoryRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("AGENTUP_REPOSITORY_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
            return FindRepositoryRoot(configuredRoot);

        try
        {
            return FindRepositoryRoot(Directory.GetCurrentDirectory());
        }
        catch (InvalidOperationException) when (!PathsEquivalent(Directory.GetCurrentDirectory(), AppContext.BaseDirectory))
        {
            return FindRepositoryRoot(AppContext.BaseDirectory);
        }
    }

    public static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException($"Could not find repository root from {startDirectory}.");
    }

    private static bool PathsEquivalent(string left, string right)
        => string.Equals(
            Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.Ordinal);
}
