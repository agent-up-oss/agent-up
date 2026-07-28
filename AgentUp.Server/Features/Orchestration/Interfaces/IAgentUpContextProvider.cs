namespace AgentUp.Server.Features.Orchestration.Interfaces;

public interface IAgentUpContextProvider
{
    string GetAgentUpContext();

    string GetAgentUpJsonFormat();
}
