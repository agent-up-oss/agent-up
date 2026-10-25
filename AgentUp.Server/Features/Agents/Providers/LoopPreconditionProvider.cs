using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.DTOs;
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
        var status = await StatusAsync(workspaceId, cancellationToken);
        var unmet = status.FirstOrDefault(item => !item.Satisfied);
        return unmet is null ? null : AutonomyPreconditions.Refusal(unmet.Id);
    }

    public async Task<IReadOnlyList<AutonomyPreconditionDto>> StatusAsync(
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return [Item(AutonomyPreconditions.WorkspaceRegistered, false)];

        var bound = !string.IsNullOrWhiteSpace(workspace.BoundAgentModuleId);
        var supportsPermissions = bound && agents.SupportsPermissionRequests(workspace.BoundAgentModuleId!);
        var current = settings.Get(workspaceId);
        var policy = current.PolicyRules.Count > 0;
        var hostAuthorized = HostAuthorized(workspaceId, current.HostConnectionAuthorized);

        var head = await git.GetHeadAsync(workspaceId, cancellationToken);
        var branch = head is not null
            && !string.IsNullOrWhiteSpace(head.Branch)
            && !string.Equals(head.Branch, "not on a git branch", StringComparison.Ordinal);
        var pushed = branch && head!.Ahead == 0;

        var changes = await git.GetChangesAsync(workspaceId, cancellationToken);
        var commitStatus = await commits.GetStatusAsync(workspace.WorktreePath, cancellationToken);
        var noEdit = commitStatus.ActiveSession is null;
        var assigned = changes is not { FileCount: > 0 } || commitStatus.UnassignedFiles.Count == 0;

        return [
            Item(AutonomyPreconditions.WorkspaceRegistered, true),
            Item(AutonomyPreconditions.BoundAgent, bound),
            Item(AutonomyPreconditions.PermissionRequests, supportsPermissions),
            Item(AutonomyPreconditions.PermissionPolicy, policy),
            Item(AutonomyPreconditions.HostConnectionAuthorized, hostAuthorized),
            Item(AutonomyPreconditions.GitBranch, branch),
            Item(AutonomyPreconditions.UnpushedCommits, pushed),
            Item(AutonomyPreconditions.CommitQueueEdit, noEdit),
            Item(AutonomyPreconditions.UnassignedChanges, assigned)
        ];
    }

    private static AutonomyPreconditionDto Item(string id, bool satisfied) =>
        new(id, satisfied, satisfied ? null : AutonomyPreconditions.Remedy(id));

    private bool HostAuthorized(string workspaceId, bool flagged) =>
        host?.IsPresent(workspaceId) ?? flagged;
}
