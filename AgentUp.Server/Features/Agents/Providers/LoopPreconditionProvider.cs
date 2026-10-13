using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Commits.Controllers;
using AgentUp.Server.Features.Git.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class LoopPreconditionProvider(
    WorkspaceQueryController workspaces,
    GitQueryController git,
    CommitsController commits,
    IAgentWorkspaceSettingsRepository settings,
    IEnabledAgentInspector agents)
{
    public async Task<string?> UnmetAsync(string workspaceId, CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return "The workspace is not registered.";
        if (string.IsNullOrWhiteSpace(workspace.BoundAgentModuleId))
            return "An agent is not bound to the workspace.";
        if (!agents.SupportsPermissionRequests(workspace.BoundAgentModuleId))
            return "The bound agent module does not declare support for protocol permission requests.";
        var current = settings.Get(workspaceId);
        if (current.PolicyRules.Count == 0)
            return "A permission policy is not configured.";
        if (!current.HostConnectionAuthorized)
            return "A repository host connection is not authorized for this repository.";

        var head = await git.GetHeadAsync(workspaceId, cancellationToken);
        if (head is null
            || string.IsNullOrWhiteSpace(head.Branch)
            || string.Equals(head.Branch, "not on a git branch", StringComparison.Ordinal))
            return "The workspace is not on a git branch.";
        if (head.Ahead > 0)
            return "The branch has unpushed commits.";

        var changes = await git.GetChangesAsync(workspaceId, cancellationToken);
        var status = await commits.GetStatusAsync(workspace.WorktreePath, cancellationToken);
        if (status.ActiveSession is not null)
            return "A commit queue edit session is active.";
        if (changes is { FileCount: > 0 } && status.UnassignedFiles.Count > 0)
            return "The working tree has changes that are not assigned to a queued proposal.";
        return null;
    }
}
