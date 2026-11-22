using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Files.Interfaces;

namespace AgentUp.Server.Features.Files.Providers;

public sealed class WorkspaceAgentEditGuard(AgentsController agents) : IWorkspaceFileEditGuard
{
    public bool IsAgentWorking(string workspaceId) =>
        string.Equals(agents.Get(workspaceId)?.State, "running", StringComparison.Ordinal);
}
