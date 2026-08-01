using AgentUp.Server.Features.Audit.Interfaces;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Audit.Providers;

public sealed class AuditIdentityProvider(
    AuditWorkdirIdProvider workdirIds,
    AuditGitStateProvider git) : IAuditIdentityProvider
{
    public async Task<AuditIdentity> ReadAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        var worktreePath = workspace.WorktreePath;
        var workdirId = string.IsNullOrWhiteSpace(worktreePath) ? null : workdirIds.Create(worktreePath);
        var gitState = string.IsNullOrWhiteSpace(worktreePath)
            ? (Branch: (string?)null, Commit: (string?)null, Dirty: (bool?)null)
            : await git.ReadAsync(worktreePath, cancellationToken);

        return new AuditIdentity(
            workspace.RepositoryPath,
            workspace.WorktreePath,
            workdirId,
            gitState.Branch ?? workspace.Branch,
            gitState.Commit ?? workspace.Commit,
            gitState.Dirty);
    }
}
