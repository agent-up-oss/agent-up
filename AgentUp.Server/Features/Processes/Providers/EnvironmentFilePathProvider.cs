namespace AgentUp.Server.Features.Processes.Providers;

public static class EnvironmentFilePathProvider
{
    public static IEnumerable<string> ReadExistingWorkspaceFileLines(string worktreePath, string environmentFile)
        => File.ReadLines(ResolveExistingWorkspaceFile(worktreePath, environmentFile));

    public static string ResolveExistingWorkspaceFile(string worktreePath, string environmentFile)
    {
        var safeFullPath = WorkspacePathProvider.ResolveWorkspaceRootFile(
            worktreePath,
            environmentFile,
            "Environment file");
        if (!File.Exists(safeFullPath))
            throw new InvalidOperationException($"Environment file '{environmentFile}' was not found.");

        return safeFullPath;
    }
}
