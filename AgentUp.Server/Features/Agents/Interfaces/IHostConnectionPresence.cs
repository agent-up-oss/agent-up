namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IHostConnectionPresence
{
    bool IsPresent();

    bool IsPresent(string workspaceId);
}
