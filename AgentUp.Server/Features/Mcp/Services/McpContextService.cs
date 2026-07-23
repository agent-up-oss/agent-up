using AgentUp.Server.Features.Mcp.Interfaces;

namespace AgentUp.Server.Features.Mcp.Services;

public sealed class McpContextService
{
    private readonly IAgentUpContextProvider _context;

    public McpContextService(IAgentUpContextProvider context)
    {
        _context = context;
    }

    public string GetAgentUpContext() => _context.GetAgentUpContext();

    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();
}
