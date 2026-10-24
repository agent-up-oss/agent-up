using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class LoopPreconditionProvider(
    WorkspaceQueryController workspaces,
    GitQueryController git,
    CommitsController commits,
    IAgentWorkspaceSettingsRepository settings,
    IEnabledAgentInspector agents,
    IHostConnectionPresence? host = null)
{
    public async Task<string?> UnmetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.WorkspaceRegistered);
        if (string.IsNullOrWhiteSpace(workspace.BoundAgentModuleId))
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.BoundAgent);
        if (!agents.SupportsPermissionRequests(workspace.BoundAgentModuleId))
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.PermissionRequests);
        var current = settings.Get(workspaceId);
        if (current.PolicyRules.Count == 0)
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.PermissionPolicy);
        if (!HostAuthorized(workspaceId, current.HostConnectionAuthorized))
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.HostConnectionAuthorized);

        var head = await git.GetHeadAsync(workspaceId, cancellationToken);
        if (head is null
            || string.IsNullOrWhiteSpace(head.Branch)
            || string.Equals(head.Branch, "not on a git branch", StringComparison.Ordinal))
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.GitBranch);
        if (head.Ahead > 0)
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.UnpushedCommits);

        var changes = await git.GetChangesAsync(workspaceId, cancellationToken);
        var status = await commits.GetStatusAsync(workspace.WorktreePath, cancellationToken);
        if (status.ActiveSession is not null)
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.CommitQueueEdit);
        if (changes is { FileCount: > 0 } && status.UnassignedFiles.Count > 0)
            return AutonomyPreconditions.Refusal(AutonomyPreconditions.UnassignedChanges);
        return null;
    }

    private bool HostAuthorized(string workspaceId, bool flagged) =>
        host?.IsPresent(workspaceId) ?? flagged;
}
