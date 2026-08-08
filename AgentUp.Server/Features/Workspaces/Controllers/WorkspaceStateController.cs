using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Workspaces.Controllers;

public sealed class WorkspaceStateController
{
    private readonly WorkspaceRegistry _registry;
    private readonly WorkspaceEventBus _bus;

    public WorkspaceStateController(WorkspaceRegistry registry, WorkspaceEventBus bus)
    {
        _registry = registry;
        _bus = bus;
    }

    public async Task<bool> UpdateApplicationStateAsync(string workspaceId, string applicationName, ApplicationState state)
        => await _registry.UpdateApplicationStateAsync(workspaceId, applicationName, state);

    public async Task<bool> UpdateWorkspaceStateAsync(string workspaceId, WorkspaceState state)
        => await _registry.UpdateStateAsync(workspaceId, state);

    public void SetApplicationHealthState(string workspaceId, string appName, ApplicationState state)
    {
        var workspace = _registry.GetById(workspaceId);
        var app = workspace?.Applications.FirstOrDefault(a => a.Name == appName);
        if (app is not null) app.State = state;
    }

    public void PublishEnrichedEvent(
        string workspaceId,
        IReadOnlyList<AppStateChange> appChanges,
        string? workspaceHealth)
    {
        var workspace = _registry.GetById(workspaceId);
        if (workspace is null) return;
        _bus.Publish(new WorkspaceStateChangedEvent(
            workspaceId,
            workspace.State.ToString(),
            appChanges,
            workspaceHealth));
    }
}
