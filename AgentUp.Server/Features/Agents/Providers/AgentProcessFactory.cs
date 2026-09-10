using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentProcessFactory(AgentCommandProvider commands, ILoggerFactory loggerFactory)
{
    public IAgentProcessProvider Create() =>
        new AcpProcessProvider(commands, loggerFactory.CreateLogger<AcpProcessProvider>());
}
