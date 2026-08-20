using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Models;

namespace AgentUp.Server.Features.Applications.Controllers;

public sealed class AppHealthController(AppHealthCheckService healthChecks)
{
    // (workspaceId, appName, allocatedPort, isHealthy)
    public event Action<string, string, int, bool>? PortHealthChanged
    {
        add => healthChecks.PortHealthChanged += value;
        remove => healthChecks.PortHealthChanged -= value;
    }

    public void StartForWorkspace(Workspace workspace) => healthChecks.StartForWorkspace(workspace);

    public void StopForWorkspace(string workspaceId) => healthChecks.StopForWorkspace(workspaceId);

    public IReadOnlyList<PortHealthChange>? GetPortHealth(string workspaceId, string appName)
        => healthChecks.GetPortHealth(workspaceId, appName);

    public string? GetWorkspaceHealth(string workspaceId)
        => healthChecks.GetWorkspaceHealth(workspaceId);
}
