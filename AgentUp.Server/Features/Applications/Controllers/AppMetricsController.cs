using AgentUp.Server.Features.Applications.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Applications.Controllers;

public sealed class AppMetricsController(AppMetricsPullService metricsPulls)
{
    public void StartForWorkspace(Workspace workspace) => metricsPulls.StartForWorkspace(workspace);

    public void StopForWorkspace(string workspaceId) => metricsPulls.StopForWorkspace(workspaceId);
}
