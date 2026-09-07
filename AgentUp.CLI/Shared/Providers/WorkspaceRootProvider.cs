namespace AgentUp.CLI.Shared.Providers;

public static class WorkspaceRootProvider
{
    public static string? Find(string startingDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startingDirectory));
        while (directory is not null)
        {
            if (File.Exists(Path.Join(directory.FullName, "agent-up.json")))
                return directory.FullName;

            directory = directory.Parent;
        }

        return null;
    }
}
