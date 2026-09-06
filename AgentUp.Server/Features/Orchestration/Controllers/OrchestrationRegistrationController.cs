using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Orchestration.Controllers;

public sealed class OrchestrationRegistrationController(OrchestrationRegistrationService registration)
{
    public Task<RegisterWorkspaceRequest?> BuildAsync(string worktreePath, CancellationToken cancellationToken)
        => registration.BuildAsync(worktreePath, cancellationToken);
}
