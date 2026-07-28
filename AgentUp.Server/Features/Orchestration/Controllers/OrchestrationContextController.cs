using AgentUp.Server.Features.Orchestration.Services;

namespace AgentUp.Server.Features.Orchestration.Controllers;

public sealed class OrchestrationContextController
{
    private readonly OrchestrationContextService _context;

    public OrchestrationContextController(OrchestrationContextService context) => _context = context;

    public string GetAgentUpContext() => _context.GetAgentUpContext();

    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();
}
