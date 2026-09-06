using AgentUp.Server.Features.Orchestration.Interfaces;
using AgentUp.Server.Features.Orchestration.Providers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Orchestration.Services;

public sealed class OrchestrationRegistrationService(
    IAgentUpConfigurationProvider configuration,
    IWorkspaceIdentityProvider identity)
{
    public async Task<RegisterWorkspaceRequest?> BuildAsync(
        string worktreePath,
        CancellationToken cancellationToken)
    {
        var config = await configuration.LoadAsync(worktreePath, cancellationToken);
        if (config is null)
            return null;

        var workspaceIdentity = await identity.ReadAsync(worktreePath, cancellationToken);
        return WorkspaceRegistrationBuilder.Build(config, workspaceIdentity, worktreePath);
    }
}
