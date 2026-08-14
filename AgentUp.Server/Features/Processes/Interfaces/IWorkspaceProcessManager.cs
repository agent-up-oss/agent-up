using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Processes.Interfaces;

public interface IWorkspaceProcessManager
{
    Task LaunchAsync(Workspace workspace);
    Task LaunchApplicationAsync(Workspace workspace, string appName);
    Task KillAsync(string workspaceId);
    Task KillApplicationAsync(string workspaceId, string appName);
}
