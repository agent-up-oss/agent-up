using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Workspaces.Controllers;

public sealed class WorkspaceReadinessController(WorkspaceReadinessService readiness)
{
    public Task WatchAsync(string workspaceId) => readiness.WatchAsync(workspaceId);

    public void Stop(string workspaceId) => readiness.Stop(workspaceId);
}
