using AgentUp.Server.Features.Commits.DTOs;
using AgentUp.Server.Features.Commits.Services;

namespace AgentUp.Server.Features.Commits.Controllers;

public sealed class CommitsController(CommitsService service)
{
    public Task<CommitsEnqueueResult> EnqueueAsync(string worktreePath, EnqueueRequest request, CancellationToken cancellationToken = default)
        => service.EnqueueAsync(worktreePath, request, cancellationToken);

    public Task<CommitsStatusResult> GetStatusAsync(string worktreePath, CancellationToken cancellationToken = default)
        => service.GetStatusAsync(worktreePath, cancellationToken);
}
