namespace AgentUp.AUDebug.Shared.Providers;

public static class RepositoryRootProvider
{
    public static string? Find(string startingDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startingDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }
}
