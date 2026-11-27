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
        workspaces.GetById(workspaceId) is null ? null : ToDto(settings.Get(workspaceId));

    public AgentActionResult Set(string workspaceId, TeamMemberKind kind) =>
        Set(workspaceId, new TeamMemberRequest(kind));

    public AgentActionResult Set(string workspaceId, TeamMemberRequest request)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return AgentActionResult.NotFound();
        var current = settings.Get(workspaceId);
        var kind = request.Kind;
        if (current.Member != kind)
        {
            if (current.AutonomyActive)
                return AgentActionResult.Failed("Deactivate autonomy before changing this workspace's team member kind.");

            var grants = TeamMemberGrants.For(kind);
            var bound = workspace.BoundAgentModuleId;
            if (!string.IsNullOrWhiteSpace(bound) && !grants.AllowsFixture(InProcessAgents.FixtureOf(bound)))
                return AgentActionResult.Failed($"This workspace is bound to '{bound}', which a {kind} cannot run. Unbind it first.");
            var exceeded = current.PolicyRules.Select(grants.Exceeds).FirstOrDefault(reason => reason is not null);
            if (exceeded is not null)
                return AgentActionResult.Failed(exceeded);
        }

        settings.Save(current with
        {
            Member = kind,
            Role = current.Member == kind ? RoleFor(kind, current.Role, request.SystemPrompt) : null,
            Customization = request.CustomizationPrompt ?? current.Customization
        });
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

    public static TeamMemberDto ToDto(TeamMemberGrants grants) => ToDto(grants, TeamRoles.DefaultPrompt(grants.Kind), null, TeamRoles.DefaultPrompt(grants.Kind));

    public static TeamMemberDto ToDto(AgentWorkspaceSettings current)
    {
        var grants = TeamMemberGrants.For(current.Member);
        var defaults = TeamRoles.DefaultPrompt(current.Member);
        return ToDto(grants, current.Role?.Prompt ?? defaults, current.Customization, defaults);
    }

    private static TeamMemberDto ToDto(TeamMemberGrants grants, string systemPrompt, string? customization, string defaultPrompt) => new(
        grants.Kind,
        grants.ReadsRepository,
        grants.WritesRepository,
        grants.ControlsBrowser,
        grants.SubmitsReviews,
        grants.Fixture,
        [.. grants.OperationPermissions.Order(StringComparer.Ordinal)],
        systemPrompt,
        customization,
        defaultPrompt);

    /// <summary>
    /// Stores a system prompt only when it differs from this kind's default, or when the stored
    /// role already carries tools or a non-default trigger. A type change passes null for the
    /// prompt so the new kind's default is used.
    /// </summary>
    private static AgentRoleDefinition? RoleFor(TeamMemberKind kind, AgentRoleDefinition? current, string? systemPrompt)
    {
        if (systemPrompt is null)
            return current;
        var defaults = TeamRoles.For(kind, null);
        var prompt = systemPrompt;
        var tools = current?.Tools ?? defaults.Tools;
        var trigger = current?.Trigger ?? defaults.Trigger;
        var termination = current?.Termination ?? defaults.Termination;
        var review = current?.Review ?? defaults.Review;
        if (prompt == defaults.Prompt && tools.Count == 0 && trigger == defaults.Trigger && termination == defaults.Termination)
            return null;
        return new AgentRoleDefinition(prompt, tools, trigger, termination, review);
    }
}
