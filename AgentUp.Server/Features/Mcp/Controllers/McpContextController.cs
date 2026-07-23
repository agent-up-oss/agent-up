using AgentUp.Server.Features.Mcp.Services;

namespace AgentUp.Server.Features.Mcp.Controllers;

public sealed class McpContextController
{
    private readonly McpContextService _context;

    public McpContextController(McpContextService context) => _context = context;

    public string GetAgentUpContext() => _context.GetAgentUpContext();

    public string GetAgentUpJsonFormat() => _context.GetAgentUpJsonFormat();
}
