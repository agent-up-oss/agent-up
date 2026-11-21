using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Owns which team member a workspace is. Changing the kind is refused while it would leave the
/// workspace holding more than the new kind allows: a bound agent of the wrong fixture, an allow
/// rule outside the grant, or an active unattended loop.
/// </summary>
public sealed class TeamMemberService(IAgentWorkspaceSettingsRepository settings, WorkspaceQueryController workspaces)
{
    public TeamMemberKind Kind(string workspaceId) => settings.Get(workspaceId).Member;

    public TeamMemberGrants Grants(string workspaceId) => TeamMemberGrants.For(Kind(workspaceId));

    public TeamMemberDto? Describe(string workspaceId) =>
        workspaces.GetById(workspaceId) is null ? null : ToDto(Grants(workspaceId));

    public AgentActionResult Set(string workspaceId, TeamMemberKind kind)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return AgentActionResult.NotFound();
        var current = settings.Get(workspaceId);
        if (current.Member == kind)
            return AgentActionResult.Success();
        if (current.AutonomyActive)
            return AgentActionResult.Failed("Deactivate autonomy before changing this workspace's team member kind.");

        var grants = TeamMemberGrants.For(kind);
        var bound = workspace.BoundAgentModuleId;
        if (!string.IsNullOrWhiteSpace(bound) && !grants.AllowsFixture(InProcessAgents.FixtureOf(bound)))
            return AgentActionResult.Failed($"This workspace is bound to '{bound}', which a {kind} cannot run. Unbind it first.");
        var exceeded = current.PolicyRules.Select(grants.Exceeds).FirstOrDefault(reason => reason is not null);
        if (exceeded is not null)
            return AgentActionResult.Failed(exceeded);

        settings.Save(current with { Member = kind });
        return AgentActionResult.Success();
    }

    /// <summary>
    /// Review threads are work another member must address before a proposal can land, so only
    /// reviewers raise them. Authors still comment; they do not review.
    /// </summary>
    public string? ReviewRefusal(string workspaceId)
    {
        var grants = Grants(workspaceId);
        return grants.SubmitsReviews
            ? null
            : $"A {grants.Kind} team member cannot submit reviews. Only DevReviewer and PmReviewer workspaces raise review threads.";
    }

    public static TeamMemberDto ToDto(TeamMemberGrants grants) => new(
        grants.Kind,
        grants.ReadsRepository,
        grants.WritesRepository,
        grants.ControlsBrowser,
        grants.SubmitsReviews,
        grants.Fixture,
        [.. grants.OperationPermissions.Order(StringComparer.Ordinal)]);
}
