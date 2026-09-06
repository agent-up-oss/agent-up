using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Orchestration.Providers;

public static class WorkspaceRegistrationBuilder
{
    public static RegisterWorkspaceRequest Build(
        AgentUpConfiguration config,
        WorkspaceIdentity identity,
        string worktreePath)
    {
        var displayName = string.IsNullOrWhiteSpace(config.Display?.Name) ? config.Name : config.Display!.Name;
        var branch = string.IsNullOrWhiteSpace(config.Display?.Branch) ? identity.Branch : config.Display!.Branch;

        return new RegisterWorkspaceRequest(
            DisplayName: displayName,
            RepositoryPath: identity.RepositoryPath,
            WorktreePath: worktreePath,
            Branch: branch,
            Commit: identity.Commit)
        {
            Applications = config.Applications ?? [],
            Services = config.Services ?? []
        };
    }
}
