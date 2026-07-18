using AgentUp.Server.Features.Mcp.Interfaces;

namespace AgentUp.Server.Features.Mcp.Controllers;

public sealed class McpContextController
{
    private readonly IAgentUpContextProvider _context;

    public McpContextController(IAgentUpContextProvider context) => _context = context;

    public string GetAgentUpContext() => _context.GetAgentUpContext();

    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();
}
