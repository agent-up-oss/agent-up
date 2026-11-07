namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAgentProcessFactory
{
    IAgentProcessProvider Create();

    /// <summary>
    /// The process for one agent in one workspace. ACP coding CLIs ignore both; an in-process
    /// agent needs the workspace to pin every tool call it makes.
    /// </summary>
    IAgentProcessProvider Create(string agent, string workspaceId) => Create();
}
