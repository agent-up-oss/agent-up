using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Git.Interfaces;

namespace AgentUp.Server.Features.Git.Providers;

public sealed class WorkspaceAgentPromptGuard(AgentsController agents) : IWorkspacePromptGuard
{
    public bool IsPromptRunning(string workspaceId) =>
        string.Equals(agents.Get(workspaceId)?.State, "running", StringComparison.Ordinal);
}
