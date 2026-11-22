namespace AgentUp.Server.Features.Files.Interfaces;

/// <summary>Whether a person may change a workspace's files right now.</summary>
public interface IWorkspaceFileEditGuard
{
    /// <summary>True while the workspace's agent is in a turn, which a hand edit would race.</summary>
    bool IsAgentWorking(string workspaceId);
}
