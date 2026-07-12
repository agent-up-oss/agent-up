using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Services;

public interface IWorkspaceRegistry
{
    IReadOnlyList<Workspace> GetAll();
    Workspace? GetById(string id);
    Task<Workspace> RegisterAsync(RegisterWorkspaceRequest request);
    Task<bool> UpdateStateAsync(string id, WorkspaceState state);
    Task<bool> UpdateApplicationStateAsync(string workspaceId, string appName, ApplicationState state);
    Task<bool> RemoveAsync(string id);
}
