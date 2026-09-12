namespace AgentUp.Server.Features.Git.Interfaces;

public interface IWorkspacePromptGuard
{
    bool IsPromptRunning(string workspaceId);
}
