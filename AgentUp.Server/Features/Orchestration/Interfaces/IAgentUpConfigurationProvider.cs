using AgentUp.Server.Features.Orchestration.DTOs;

namespace AgentUp.Server.Features.Orchestration.Interfaces;

public interface IAgentUpConfigurationProvider
{
    Task<AgentUpConfiguration?> LoadAsync(string worktreePath, CancellationToken cancellationToken);
}
