using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IRoleToolFilter
{
    void Restrict(McpServerOptions options, IReadOnlyList<string> allowlist);
}
