using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Workspaces.Controllers;

public sealed class WorkspaceLifecycleController(WorkspaceLifecycleService lifecycle)
{
    public Task<WorkspaceLifecycleResult> StartAsync(string id) => lifecycle.StartAsync(id);

    public Task<WorkspaceLifecycleResult> StopAsync(string id) => lifecycle.StopAsync(id);
}
