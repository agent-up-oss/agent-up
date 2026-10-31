using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// One agent as a row. Every string here is derived from the standing the Server reported; where it
/// reported nothing the row says so rather than filling the space.
/// </summary>
public sealed class StandingRowViewModel
{
    public StandingRowViewModel(TeamStanding standing)
    {
        Standing = standing;
    }

    public TeamStanding Standing { get; }

    public TeamMember Member => new(Standing.WorkspaceId, Standing.DisplayName, Standing.Branch, Standing.WorkspaceState);

    public string WorkspaceId => Standing.WorkspaceId;
    public string DisplayName => Standing.DisplayName;
    public string Initial => Standing.Initial;
    public string Branch => Standing.Branch;

    /// <summary>
    /// Who this agent is, under their name: what they are for, which account they act as, and which
    /// model drives them. Each part is dropped when the Server did not report it, so the line never
    /// pads itself out with placeholders.
    /// </summary>
    public string Subtitle => string.Join(" · ", SubtitleParts());

    /// <summary>What this agent is for, or an empty string while no role is configured.</summary>
    public string RoleLabel => Standing.RoleLabel ?? string.Empty;

    public bool HasRole => Standing.RoleLabel is { Length: > 0 };

    /// <summary>The account the agent acts as on its host, as a reviewer sees it on a thread.</summary>
    public string IdentityHandle => Standing.Identity?.Handle ?? string.Empty;

    public bool HasIdentity => Standing.Identity is not null;

    public string IdentityToolTip => Standing.Identity is null
        ? string.Empty
        : $"{Standing.Identity.DisplayName} · {Standing.Identity.Url}";

    public string StateLabel => Standing.Kind switch
    {
        TeamStandingKind.WaitingOnYou => "◆ gate open",
        TeamStandingKind.Halted => "▲ halted",
        TeamStandingKind.Working => "● working",
        _ => "○ idle"
    };

    public string StateColor => Standing.Kind switch
    {
        TeamStandingKind.WaitingOnYou => AgentUpThemeColors.StatusWarning,
        TeamStandingKind.Halted => AgentUpThemeColors.StatusDanger,
        TeamStandingKind.Working => AgentUpThemeColors.AccentBright,
        _ => AgentUpThemeColors.TextMuted
    };

    public string TaskLabel => Standing.TaskLabel ?? "no task in hand";

    public bool HasTask => Standing.TaskNumber is not null;

    /// <summary>
    /// What this agent is doing, in their own terms. A reviewer never works on an issue — the
    /// branch is somebody else's and the only thing they produce is a review — so saying they are
    /// "working on" it would claim work that is not theirs.
    /// </summary>
    public string Headline => Standing.Kind switch
    {
        // What a gate needs is yours whoever holds it, so this one reads the same for either role.
        TeamStandingKind.WaitingOnYou => $"{DisplayName} is waiting on you{OnTask}",
        TeamStandingKind.Halted => $"{DisplayName} stopped {Verb}{TaskSuffix}",
        TeamStandingKind.Working => $"{DisplayName} is {Verb}{TaskSuffix}",
        _ => $"{DisplayName} has nothing in hand"
    };

    /// <summary>
    /// What this agent does to a task: a reviewer reviews it, everyone else works on it. The
    /// preposition belongs to the suffix, so "reviewing #44" and "working on #44" both read.
    /// </summary>
    public string Verb => Standing.ReviewsOthersWork ? "reviewing" : "working";

    public string? Reason => Standing.Reason;

    public bool HasReason => Standing.Reason is { Length: > 0 };

    public string? DiagnosticCode => Standing.DiagnosticCode;

    public bool HasDiagnosticCode =>
        Standing.Kind == TeamStandingKind.Halted && Standing.DiagnosticCode is { Length: > 0 };

    public bool HasProposal => Standing.Forge.HasProposal;

    public string ProposalLabel => Standing.Forge.HasProposal
        ? $"#{Standing.Forge.ProposalNumber} {Standing.Forge.ProposalTitle}"
        : string.Empty;

    public string? ProposalUrl => Standing.Forge.ProposalUrl;

    /// <summary>
    /// Whether the proposal is worth naming next to the task. A proposal opened for the task it is
    /// listed under repeats it word for word; one opened under a different number does not.
    /// </summary>
    public bool ShowProposalBesideTask =>
        HasProposal && Standing.Forge.ProposalNumber != Standing.TaskNumber?.ToString();

    public bool HasUnresolvedThreads => Standing.Forge.UnresolvedThreads > 0;

    public string ThreadSummary => Standing.Forge.UnresolvedThreads == 1
        ? "1 unresolved review thread"
        : $"{Standing.Forge.UnresolvedThreads} unresolved review threads";

    private string OnTask => Standing.TaskNumber is null ? string.Empty : $" on #{Standing.TaskNumber}";

    private string TaskSuffix => Standing.TaskNumber is null
        ? string.Empty
        : Standing.ReviewsOthersWork ? $" #{Standing.TaskNumber}" : OnTask;

    /// <summary>
    /// How the checks stand, as "(passed/total)". The dot beside it carries the verdict, so the
    /// numbers only have to say how much of the run is accounted for.
    /// </summary>
    public string ChecksCount => $"({Standing.Forge.ChecksPassed}/{Standing.Forge.ChecksTotal})";

    public string ChecksColor => Standing.Forge.Checks switch
    {
        ChecksState.Failed => AgentUpThemeColors.StatusDanger,
        ChecksState.Running => AgentUpThemeColors.StatusWarning,
        ChecksState.Passed => AgentUpThemeColors.AccentBright,
        _ => AgentUpThemeColors.TextFaint
    };

    public string ChecksToolTip => Standing.Forge.Checks switch
    {
        ChecksState.Failed => $"{Standing.Forge.ChecksFailed} of {Standing.Forge.ChecksTotal} checks failed",
        ChecksState.Running => $"{Standing.Forge.ChecksRunning} of {Standing.Forge.ChecksTotal} checks still running",
        ChecksState.Passed => $"all {Standing.Forge.ChecksTotal} checks passed",
        ChecksState.Skipped => $"all {Standing.Forge.ChecksTotal} checks were skipped",
        _ => "no checks reported"
    };

    /// <summary>The two review slots, drawn only when there is a proposal to review.</summary>
    public IReadOnlyList<ReviewSlotViewModel> Reviews =>
        [.. Standing.Slots.Select(slot => new ReviewSlotViewModel(slot))];

    /// <summary>
    /// True while this agent holds a proposal of its own. A reviewer has one on its branch too,
    /// but it belongs to whoever opened it, so the row would report somebody else's standing.
    /// </summary>
    public bool HasStatusRow => Standing.Forge.HasProposal && !Standing.ReviewsOthersWork;

    /// <summary>
    /// Who this agent is: what they are for, which account they act as, and which model drives
    /// them. The workspace's own lifecycle state is not among them. It says whether Agent-Up has
    /// the workspace running, not what the agent is doing, so a running workspace whose agent is
    /// idle would read "Running" here and "idle" on the status line below. What the agent is
    /// doing is <see cref="StateLabel"/>.
    /// </summary>
    private IEnumerable<string> SubtitleParts()
    {
        if (Standing.RoleLabel is { Length: > 0 } role) yield return role;
        if (Standing.Identity is { } identity) yield return identity.Handle;
        if (Standing.Agent is { Length: > 0 } agent) yield return agent;
    }

}
