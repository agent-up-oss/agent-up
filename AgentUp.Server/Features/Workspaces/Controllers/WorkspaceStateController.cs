using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Workspaces.Controllers;

public sealed class WorkspaceStateController
{
    private readonly WorkspaceRegistry _registry;

    public WorkspaceStateController(WorkspaceRegistry registry)
    {
        _registry = registry;
    }

    public async Task<bool> UpdateApplicationStateAsync(string workspaceId, string applicationName, ApplicationState state)
        => await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, state);

    public async Task<bool> UpdateWorkspaceStateAsync(string workspaceId, WorkspaceState state)
        => await _registry.UpdateStateAsync(workspaceId, state);
}
