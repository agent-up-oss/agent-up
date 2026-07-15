namespace AgentUp.Packaging.Features.ReleaseArtifacts;

public static class RepositoryPaths
{
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
}
