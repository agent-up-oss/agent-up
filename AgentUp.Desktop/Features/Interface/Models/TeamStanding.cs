namespace AgentUp.Desktop.Features.Interface.Models;

/// <summary>
/// What one agent needs from you. The bridge groups by this and nothing else, so a surface never
/// has to decide for itself which of two reads wins.
/// </summary>
public enum TeamStandingKind
{
    /// <summary>No run, or a run that ended with nothing left to take.</summary>
    Idle,

    /// <summary>A run is in flight and needs nothing.</summary>
    Working,

    /// <summary>A run stopped at a gate and will not move until you answer.</summary>
    WaitingOnYou,

    /// <summary>A run ended on a fault. Someone has to look.</summary>
    Halted
}

/// <summary>
/// The workspace a standing is read for. Only the four fields a triage row shows: the standing
/// itself comes from the Server, not from here.
/// </summary>
public sealed record TeamMember(string WorkspaceId, string DisplayName, string Branch, string State);

/// <summary>
/// What an agent is for, as the Server's role trigger and termination say. A role that pairs them
/// any other way is named from its two halves rather than forced into one of these.
/// </summary>
public enum TeamRoleKind
{
    /// <summary>No role is configured for this workspace.</summary>
    None,

    /// <summary>Takes an assigned issue and ends by opening a proposal.</summary>
    Developer,

    /// <summary>Takes a proposal awaiting review and ends by submitting one.</summary>
    Reviewer,

    /// <summary>A trigger and termination the two archetypes do not cover.</summary>
    Other
}

/// <summary>
/// Which review a reviewing role supplies. A proposal needs one of each before it is settled, so
/// the two are slots rather than a list.
/// </summary>
public enum ReviewSlotKind
{
    /// <summary>The review of the change itself: what it does and whether it holds up.</summary>
    Dev,

    /// <summary>The review of the result: whether what was built is what was asked for.</summary>
    Pm
}

/// <summary>What a reviewer left in their slot, if anything yet.</summary>
public enum ReviewMark
{
    /// <summary>Nobody has reviewed for this slot.</summary>
    None,

    /// <summary>Approved.</summary>
    Approved,

    /// <summary>Reviewed, with findings that do not block.</summary>
    Warning,

    /// <summary>Changes requested: findings that block.</summary>
    Blocked
}

/// <summary>One of the two review slots on a proposal, and what fills it.</summary>
public sealed record ReviewSlot(ReviewSlotKind Kind, ReviewMark Mark)
{
    public string Label => Kind == ReviewSlotKind.Dev ? "dev" : "pm";

    /// <summary>The mark as one character, or a dash while the slot is still empty.</summary>
    public string Glyph => Mark switch
    {
        ReviewMark.Approved => "✓",
        ReviewMark.Warning => "!",
        ReviewMark.Blocked => "✗",
        _ => "–"
    };
}

/// <summary>
/// How a proposal's checks stand as one signal. Failing beats running beats passing, because a
/// surface that reports a run in flight while something has already failed reads as healthier
/// than it is.
/// </summary>
public enum ChecksState
{
    /// <summary>No checks were reported.</summary>
    None,

    /// <summary>Every check that ran was skipped or excluded.</summary>
    Skipped,

    /// <summary>At least one check is still running, and none has failed.</summary>
    Running,

    /// <summary>Everything that ran passed.</summary>
    Passed,

    /// <summary>At least one check failed.</summary>
    Failed
}

/// <summary>
/// The account a workspace acts as on its repository host. One workspace keeps one account, so
/// everything its agent pushes, comments, or reviews is attributable to this name.
/// </summary>
public sealed record TeamIdentity(string Login, string DisplayName, bool IsBot, string Url)
{
    /// <summary>The account as a host writes it, which is how a reviewer will see it on a thread.</summary>
    public string Handle => Login.StartsWith('@') ? Login : $"@{Login}";
}

/// <summary>
/// What the repository host reports for one workspace, counted. The counts are what a triage
/// surface shows; the lists behind them stay in the agent's own Git tab.
/// </summary>
public sealed record ForgeStanding(
    string Availability,
    string? ProposalNumber,
    string? ProposalTitle,
    string? ProposalUrl,
    int ChecksPassed,
    int ChecksFailed,
    int ChecksRunning,
    int UnresolvedThreads,
    string? MergeState,
    string? Detail,
    int ChecksSkipped = 0,
    IReadOnlyList<ForgeVerdict>? Reviews = null)
{
    public static ForgeStanding None { get; } =
        new("none", null, null, null, 0, 0, 0, 0, null, null);

    /// <summary>True once the host answered with a proposal to show.</summary>
    public bool HasProposal => ProposalNumber is { Length: > 0 };

    /// <summary>Every check the host reported, however it ended.</summary>
    public int ChecksTotal => ChecksPassed + ChecksFailed + ChecksRunning + ChecksSkipped;

    /// <summary>Where the checks stand as one signal.</summary>
    public ChecksState Checks => ChecksFailed > 0 ? ChecksState.Failed
        : ChecksRunning > 0 ? ChecksState.Running
        : ChecksPassed > 0 ? ChecksState.Passed
        : ChecksSkipped > 0 ? ChecksState.Skipped
        : ChecksState.None;

    public IReadOnlyList<ForgeVerdict> Verdicts => Reviews ?? [];
}

/// <summary>A verdict on the proposal, as the host reported it, before it is matched to a slot.</summary>
public sealed record ForgeVerdict(string Login, string State);

/// <summary>
/// Where one agent stands, composed from the two reads a Server answers per workspace: its agent
/// session and its forge. Nothing here is invented — a field is null when the Server said nothing.
/// </summary>
public sealed record TeamStanding(
    string WorkspaceId,
    string DisplayName,
    string Branch,
    string WorkspaceState,
    string? Agent,
    TeamStandingKind Kind,
    int? TaskNumber,
    string? TaskTitle,
    string? Reason,
    string? DiagnosticCode,
    IReadOnlyList<string> PermissionRuleIds,
    ForgeStanding Forge,
    TeamRoleKind Role = TeamRoleKind.None,
    IReadOnlyList<string>? RoleTools = null,
    TeamIdentity? Identity = null,
    ReviewSlotKind? Reviews = null,
    IReadOnlyList<ReviewSlot>? ReviewSlots = null)
{
    /// <summary>
    /// The two review slots on this agent's proposal, empty when there is no proposal to review.
    /// They are always both present or both absent: a missing slot is a slot nobody has filled,
    /// which is a different thing from there being nothing to review.
    /// </summary>
    public IReadOnlyList<ReviewSlot> Slots => ReviewSlots ?? [];

    /// <summary>What this agent is for, in one word, or null while no role is configured.</summary>
    public string? RoleLabel => Role switch
    {
        TeamRoleKind.Developer => "Developer",
        TeamRoleKind.Reviewer => "Reviewer",
        TeamRoleKind.Other => "Custom role",
        _ => null
    };

    /// <summary>The tools the role is allowed, which is the whole of what this agent may do.</summary>
    public IReadOnlyList<string> Tools => RoleTools ?? [];

    /// <summary>The agent's initial, for the avatar the design draws.</summary>
    public string Initial => DisplayName.Length > 0 ? DisplayName[..1].ToUpperInvariant() : "?";

    /// <summary>The task as the forge numbers it, or null while no task is in hand.</summary>
    public string? TaskLabel => TaskNumber is null
        ? null
        : TaskTitle is { Length: > 0 } title ? $"#{TaskNumber} · {title}" : $"#{TaskNumber}";
}
