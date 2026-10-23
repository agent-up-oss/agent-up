using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Git.DTOs;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>
/// Composes the team's standing from the reads a Server already answers: one agent session and one
/// forge read per workspace. There is no cross-workspace queue endpoint, so the queue this produces
/// is the set of tasks the agents themselves report — which is also why it works unchanged against
/// the Demo, whose definition answers the same two routes.
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
        => await Task.WhenAll(workspaces.Select(workspace => ReadAsync(workspace, cancellationToken)));

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
        catch (HttpRequestException)
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
        catch (HttpRequestException)
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
            session?.Agent ?? session?.BoundAgent,
            Classify(session, loop),
            loop?.TaskNumber,
            loop?.TaskTitle,
            Reason(loop, forge),
            loop?.StopReason,
            loop?.PermissionRuleIds ?? [],
            forge);
    }

    private static TeamStandingKind Classify(AgentSessionDto? session, AgentLoopStatusDto? loop)
    {
        if (loop is null) return session is null ? TeamStandingKind.Idle : TeamStandingKind.Working;
        if (IsHalted(loop)) return TeamStandingKind.Halted;
        if (IsWaiting(session, loop)) return TeamStandingKind.WaitingOnYou;
        return loop.Active ? TeamStandingKind.Working : TeamStandingKind.Idle;
    }

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
            checks.Count(check => Equals(check.Conclusion, "success")),
            checks.Count(check => Equals(check.Conclusion, "failure")),
            checks.Count(check => !Equals(check.Status, "completed")),
            (hosting.Threads ?? []).Count(thread => !thread.IsResolved),
            hosting.Mergeability?.State,
            hosting.Detail);
    }

    private static bool Equals(string? left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
