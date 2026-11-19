using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentProcessFactory(
    AgentCommandProvider commands,
    ILoggerFactory loggerFactory,
    IInProcessChatModel? model = null,
    IInProcessToolHost? tools = null) : IAgentProcessFactory
{
    public IAgentProcessProvider Create() =>
        new AcpProcessProvider(commands, loggerFactory.CreateLogger<AcpProcessProvider>());

    public IAgentProcessProvider Create(string agent, string workspaceId)
    {
        if (!InProcessAgents.IsInProcess(agent))
            return Create();
        if (model is null || tools is null)
            throw new InvalidOperationException($"{agent} is an in-process agent and this Server has no in-process agent host.");
        return new InProcessAgentProcessProvider(
            workspaceId,
            model,
            tools,
            loggerFactory.CreateLogger<InProcessAgentProcessProvider>());
    }
}
