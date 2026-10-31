using System.Text.Json;
using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>
/// Composes the team's standing from the reads a Server already answers: one agent session and one
/// forge read per workspace. What it produces is the set of tasks the agents report holding, which
/// is what the team is doing now — the queue of what they will do next is a separate read, because
/// a task nobody has started yet belongs to no agent's standing. It works unchanged against the
/// Demo, whose definition answers the same two routes.
/// </summary>
public sealed class TeamStandingService
{
    private readonly AgentsController _agents;
    private readonly GitController _git;

    public TeamStandingService(AgentsController agents, GitController git)
    {
        _agents = agents;
        _git = git;
    }

    /// <summary>
    /// Reads every agent's standing. The reads are independent per workspace, so they go out
    /// together and one slow agent does not hold up the rest of the bridge; the result keeps the
    /// order it was asked in, which is the order the sidebar lists.
    /// </summary>
    public async Task<IReadOnlyList<TeamStanding>> ReadAsync(
        IReadOnlyList<TeamMember> workspaces,
        CancellationToken cancellationToken = default)
    {
        var standings = await Task.WhenAll(workspaces.Select(workspace => ReadAsync(workspace, cancellationToken)));
        return [.. standings.Select(standing => standing with { ReviewSlots = SlotsFor(standing, standings) })];
    }

    /// <summary>
    /// Fills the two review slots on one agent's proposal. A verdict names the account that left
    /// it, and every account on this Server belongs to a workspace whose role says which review it
    /// supplies, so the team is what turns a list of verdicts into slots. A verdict from an account
    /// outside the team fills no slot: it is somebody's review, but not one of these two.
    /// </summary>
    private static IReadOnlyList<ReviewSlot>? SlotsFor(TeamStanding standing, IReadOnlyList<TeamStanding> team)
    {
        if (!standing.Forge.HasProposal) return null;
        return
        [
            new ReviewSlot(ReviewSlotKind.Dev, MarkFor(ReviewSlotKind.Dev, standing, team)),
            new ReviewSlot(ReviewSlotKind.Pm, MarkFor(ReviewSlotKind.Pm, standing, team))
        ];
    }

    private static ReviewMark MarkFor(ReviewSlotKind slot, TeamStanding standing, IReadOnlyList<TeamStanding> team)
    {
        var verdict = standing.Forge.Verdicts.FirstOrDefault(item => Supplies(slot, item.Login, team));
        return verdict is null ? ReviewMark.None : Mark(verdict.State);
    }

    private static bool Supplies(ReviewSlotKind slot, string login, IReadOnlyList<TeamStanding> team)
        => team.Any(member =>
            member.Reviews == slot
            && member.Identity is { } identity
            && string.Equals(identity.Login, login, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// What a verdict means for its slot. The three a host distinguishes are the three that matter:
    /// approved settles the slot, changes requested blocks it, and a plain comment is worth reading
    /// without standing in the way. Anything else — a dismissed or still-pending review — has not
    /// been said yet.
    /// </summary>
    private static ReviewMark Mark(string state) => state.ToUpperInvariant() switch
    {
        "APPROVED" => ReviewMark.Approved,
        "CHANGES_REQUESTED" => ReviewMark.Blocked,
        "COMMENTED" => ReviewMark.Warning,
        _ => ReviewMark.None
    };

    private async Task<TeamStanding> ReadAsync(TeamMember workspace, CancellationToken cancellationToken)
    {
        var session = await ReadSessionAsync(workspace.WorkspaceId, cancellationToken);
        var hosting = await ReadHostingAsync(workspace.WorkspaceId, cancellationToken);
        return Compose(workspace, session, hosting);
    }

    private async Task<AgentSessionDto?> ReadSessionAsync(string workspaceId, CancellationToken cancellationToken)
    {
        try
        {
            return await _agents.GetAsync(workspaceId, cancellationToken);
        }
        // A refused or dropped request, a body the Server did not write properly, and a read that
        // ran out of time are this workspace's own failure: these reads go out together, so
        // anything that escapes here fails the whole team's read rather than leaving one agent
        // unreported, which is the opposite of what this service promises. Cancellation the caller
        // asked for belongs to everybody and keeps propagating.
        catch (Exception failure) when (failure is HttpRequestException or JsonException
            || (failure is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }

    private async Task<ForgeHostingDto?> ReadHostingAsync(string workspaceId, CancellationToken cancellationToken)
    {
        try
        {
            return await _git.GetHostingAsync(workspaceId, cancellationToken);
        }
        // One workspace's own failure, as above.
        catch (Exception failure) when (failure is HttpRequestException or JsonException
            || (failure is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return null;
        }
    }

    private static TeamStanding Compose(TeamMember workspace, AgentSessionDto? session, ForgeHostingDto? hosting)
    {
        var loop = session?.Loop;
        var forge = Count(hosting);
        return new TeamStanding(
            workspace.WorkspaceId,
            workspace.DisplayName,
            workspace.Branch,
            workspace.State,
            AgentName(session),
            Classify(session, loop),
            loop?.TaskNumber,
            loop?.TaskTitle,
            Reason(loop, forge),
            loop?.StopReason,
            loop?.PermissionRuleIds ?? [],
            forge,
            Classify(session?.Role),
            session?.Role?.Tools,
            Identify(hosting?.Account),
            ReviewKind(session?.Role));
    }

    /// <summary>Which review this role supplies, or null when it supplies none.</summary>
    private static ReviewSlotKind? ReviewKind(AgentRoleDto? role) => role?.Review?.ToUpperInvariant() switch
    {
        "DEV" => ReviewSlotKind.Dev,
        "PM" => ReviewSlotKind.Pm,
        _ => null
    };

    /// <summary>
    /// The agent driving this workspace, named the way its capability names itself. The session
    /// carries the module id, which is lowercase and not what anyone calls the thing.
    /// </summary>
    private static string? AgentName(AgentSessionDto? session)
    {
        var id = session?.Agent ?? session?.BoundAgent;
        if (id is null) return null;
        var descriptor = session?.Agents?.FirstOrDefault(
            item => string.Equals(item.Agent, id, StringComparison.OrdinalIgnoreCase));
        return descriptor?.DisplayName is { Length: > 0 } name ? name : id;
    }

    /// <summary>
    /// What the role is for, from the pair the Server stores. An issue in and a proposal out is a
    /// builder; a proposal in and a review out is a reviewer. Any other pairing is a role someone
    /// configured deliberately, so it is reported as its own thing rather than squeezed into one.
    /// </summary>
    private static TeamRoleKind Classify(AgentRoleDto? role) => role switch
    {
        null => TeamRoleKind.None,
        { Trigger: "IssueAssigned", Termination: "ProposalOpened" } => TeamRoleKind.Developer,
        { Trigger: "ProposalAwaitingReview", Termination: "ReviewSubmitted" } => TeamRoleKind.Reviewer,
        _ => TeamRoleKind.Other
    };

    private static TeamIdentity? Identify(ForgeAccountDto? account)
        => account is null
            ? null
            : new TeamIdentity(account.Login, account.DisplayName, account.IsBot, account.Url);

    private static TeamStandingKind Classify(AgentSessionDto? session, AgentLoopStatusDto? loop)
    {
        if (loop is null) return WithoutLoop(session);
        if (IsHalted(loop)) return TeamStandingKind.Halted;
        if (IsWaiting(session, loop)) return TeamStandingKind.WaitingOnYou;
        return loop.Active ? TeamStandingKind.Working : TeamStandingKind.Idle;
    }

    /// <summary>
    /// Where an agent stands when no unattended loop is running. The loop is the autonomous run,
    /// and an agent can be working, held at a gate or stopped without one, so the session's own
    /// state is what says which. A session merely existing is not a run in flight: an agent that
    /// has finished and is sitting ready holds nothing.
    /// </summary>
    private static TeamStandingKind WithoutLoop(AgentSessionDto? session) => session?.State?.ToUpperInvariant() switch
    {
        "RUNNING" => TeamStandingKind.Working,
        "AWAITING_INPUT" => TeamStandingKind.WaitingOnYou,
        "ERROR" => TeamStandingKind.Halted,
        _ => TeamStandingKind.Idle
    };

    private static bool IsHalted(AgentLoopStatusDto loop)
        => Equals(loop.Phase, "failed")
           || Equals(loop.Terminal, "Failed")
           || (!loop.Active && loop.StopReason is { Length: > 0 } reason && !Equals(reason, "queue_empty"));

    private static bool IsWaiting(AgentSessionDto? session, AgentLoopStatusDto loop)
        => Equals(loop.Phase, "blocked")
           || Equals(loop.Terminal, "BlockedAwaitingInput")
           || loop.BlockReason is { Length: > 0 }
           || Equals(session?.State, "awaiting_input");

    private static string? Reason(AgentLoopStatusDto? loop, ForgeStanding forge)
        => loop?.BlockReason is { Length: > 0 } blocked ? blocked
            : forge.Detail is { Length: > 0 } detail ? detail
            : loop?.StopReason;

    private static ForgeStanding Count(ForgeHostingDto? hosting)
    {
        if (hosting is null) return ForgeStanding.None;
        var checks = hosting.Checks ?? [];
        return new ForgeStanding(
            hosting.Availability,
            hosting.Proposal?.Number,
            hosting.Proposal?.Title,
            hosting.Proposal?.Url,
            checks.Count(check => Equals(check.Status, "completed") && Equals(check.Conclusion, "success")),
            checks.Count(check => Equals(check.Status, "completed") && Failed(check.Conclusion)),
            checks.Count(check => !Equals(check.Status, "completed")),
            (hosting.Threads ?? []).Count(thread => !thread.IsResolved),
            hosting.Mergeability?.State,
            hosting.Detail,
            checks.Count(check => Equals(check.Status, "completed") && Skipped(check.Conclusion)),
            [.. (hosting.Reviews ?? []).Select(review => new ForgeVerdict(review.Login, review.State))]);
    }

    /// <summary>
    /// A check that completed without running the thing it checks. It is neither a pass nor a
    /// failure, and counting it as either misreports what the host said.
    /// </summary>
    private static bool Skipped(string conclusion)
        => !Equals(conclusion, "success") && !Failed(conclusion);

    private static bool Failed(string conclusion)
        => Equals(conclusion, "failure")
           || Equals(conclusion, "timed_out")
           || Equals(conclusion, "action_required")
           || Equals(conclusion, "startup_failure");

    private static bool Equals(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
