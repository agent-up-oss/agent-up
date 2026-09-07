using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Shared.Providers;

namespace AgentUp.Server.Features.Workspaces.Providers;

public static class WorkspaceEnvironmentFilesValidator
{
    public static void Validate(string worktreePath, IEnumerable<ApplicationEnvironmentFileSource> applications)
    {
        foreach (var application in applications)
        {
            foreach (var environmentFile in application.EnvironmentFiles ?? [])
            {
                WorkspacePathProvider.ResolveWorkspaceRootFile(
                    worktreePath,
                    environmentFile,
                    "Environment file");
            }
        }
    }
}
