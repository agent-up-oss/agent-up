using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;

namespace AgentUp.Server.Features.Workspaces.Controllers;

public sealed class WorkspaceOverviewController(WorkspaceOverviewService overview)
{
    public WorkspaceOverviewDto? Get(string id) => overview.Get(id);
}
