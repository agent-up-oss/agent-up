using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Which kinds of review a repository's tasks need. The host only counts approvals; the team
/// decides what they are: one review from each kind of reviewer the team has on the repository.
/// </summary>
public sealed class TaskReviewRequirementService(
    ForgeController forge,
    WorkspaceQueryController workspaces,
    IAgentWorkspaceSettingsRepository settings)
{
    public IReadOnlyCollection<TeamMemberKind> For(string owner, string repository) =>
    [
        .. workspaces.GetAll()
            .Select(workspace => (workspace.Id, Kind: settings.Get(workspace.Id).Member))
            .Where(member => TeamRoles.Reviews(member.Kind))
            .Where(member => forge.ResolveRepository(member.Id) is { } bound
                && string.Equals(bound.Owner, owner, StringComparison.OrdinalIgnoreCase)
                && string.Equals(bound.Repository, repository, StringComparison.OrdinalIgnoreCase))
            .Select(member => member.Kind)
            .Distinct()
            .Order()
    ];
}
