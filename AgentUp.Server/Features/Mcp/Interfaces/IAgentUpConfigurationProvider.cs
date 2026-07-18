using AgentUp.Server.Features.Mcp.DTOs;

namespace AgentUp.Server.Features.Mcp.Interfaces;

public interface IAgentUpConfigurationProvider
{
    Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken);
}
