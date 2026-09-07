using AgentUp.Server.Shared.Providers;

namespace AgentUp.Server.Features.Database.Providers;

public static class DatabaseWorkspaceFileProvider
{
    public static IEnumerable<string> ReadEnvironmentFileLines(string worktreePath, string environmentFile)
    {
        var path = WorkspacePathProvider.ResolveWorkspaceRootFile(
            worktreePath,
            environmentFile,
            "Environment file");
        if (!File.Exists(path))
            return [];

        return File.ReadLines(path);
    }
}
