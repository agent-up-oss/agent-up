using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// One task in the queue, with everyone on it. A task is not one agent's: an author builds it and
/// the reviewers read what they built, so an issue with three agents on it is one row naming three
/// people, not three rows naming the same issue.
/// </summary>
public sealed class TaskRowViewModel
{
    private readonly IReadOnlyList<TeamStanding> _standings;

    public TaskRowViewModel(int number, IReadOnlyList<TeamStanding> standings)
    {
        Number = number;
        _standings = standings;
    }

    public int Number { get; }

    /// <summary>The task's title, as whichever agent holds it reports it.</summary>
    public string TaskLabel
    {
        get
        {
            var title = _standings.Select(standing => standing.TaskTitle)
                .FirstOrDefault(name => name is { Length: > 0 });
            return title is null ? $"#{Number}" : $"#{Number} · {title}";
        }
    }

    /// <summary>
    /// Where the task stands, which is the most urgent thing any of its agents needs. A task with
    /// one agent at a gate needs you, whatever the others are doing.
    /// </summary>
    public TeamStandingKind Kind => _standings
        .Select(standing => standing.Kind)
        .OrderBy(Urgency)
        .FirstOrDefault(TeamStandingKind.Idle);

    /// <summary>How soon a standing needs somebody; lower comes first in the queue.</summary>
    public int Rank => Urgency(Kind);

    public string StateLabel => Kind switch
    {
        TeamStandingKind.WaitingOnYou => "◆ gate open",
        TeamStandingKind.Halted => "▲ halted",
        TeamStandingKind.Working => "● working",
        _ => "○ idle"
    };

    public string StateColor => Kind switch
    {
        TeamStandingKind.WaitingOnYou => AgentUpThemeColors.StatusWarning,
        TeamStandingKind.Halted => AgentUpThemeColors.StatusDanger,
        TeamStandingKind.Working => AgentUpThemeColors.AccentBright,
        _ => AgentUpThemeColors.TextMuted
    };

    /// <summary>Everyone on this task, each with what they are for.</summary>
    public IReadOnlyList<TaskAgentViewModel> Agents =>
        [.. _standings.Select(standing => new TaskAgentViewModel(standing))];

    private static int Urgency(TeamStandingKind kind) => kind switch
    {
        TeamStandingKind.WaitingOnYou => 0,
        TeamStandingKind.Halted => 1,
        TeamStandingKind.Working => 2,
        _ => 3
    };
}

/// <summary>One agent on a task: who they are and what they are for.</summary>
public sealed class TaskAgentViewModel(TeamStanding standing)
{
    public string DisplayName => standing.DisplayName;

    public string RoleLabel => standing.RoleLabel ?? string.Empty;

    public bool HasRole => standing.RoleLabel is { Length: > 0 };
}
