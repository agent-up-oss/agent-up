using ModelContextProtocol.Server;

namespace AgentUp.Server.Features.Authentication.Interfaces;

public interface IMcpWorkspaceBindingFilterProvider
{
    void Attach(McpServerOptions options, string boundWorkspace);
}
