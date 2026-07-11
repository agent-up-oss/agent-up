using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Services;

public interface IWorkspaceProcessManager
{
    Task LaunchAsync(Workspace workspace);
    Task KillAsync(string workspaceId);
}
