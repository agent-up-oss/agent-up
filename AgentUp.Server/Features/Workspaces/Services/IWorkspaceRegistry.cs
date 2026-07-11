using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Services;

public interface IWorkspaceRegistry
{
    IReadOnlyList<Workspace> GetAll();
    Workspace? GetById(string id);
    Workspace Register(RegisterWorkspaceRequest request);
    bool UpdateState(string id, WorkspaceState state);
    bool Remove(string id);
}
