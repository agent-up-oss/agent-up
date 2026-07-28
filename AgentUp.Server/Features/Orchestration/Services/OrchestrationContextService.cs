using AgentUp.Server.Features.Orchestration.Interfaces;

namespace AgentUp.Server.Features.Orchestration.Services;

public sealed class OrchestrationContextService
{
    private readonly IAgentUpContextProvider _context;

    public OrchestrationContextService(IAgentUpContextProvider context)
    {
        _context = context;
    }

    public string GetAgentUpContext() => _context.GetAgentUpContext();

    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();
}
