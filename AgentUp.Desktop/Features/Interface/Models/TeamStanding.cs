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
    string? Detail)
{
    public static ForgeStanding None { get; } =
        new("none", null, null, null, 0, 0, 0, 0, null, null);

    /// <summary>True once the host answered with a proposal to show.</summary>
    public bool HasProposal => ProposalNumber is { Length: > 0 };
}

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
    ForgeStanding Forge)
{
    /// <summary>The agent's initial, for the avatar the design draws.</summary>
    public string Initial => DisplayName.Length > 0 ? DisplayName[..1].ToUpperInvariant() : "?";

    /// <summary>The task as the forge numbers it, or null while no task is in hand.</summary>
    public string? TaskLabel => TaskNumber is null
        ? null
        : TaskTitle is { Length: > 0 } title ? $"#{TaskNumber} · {title}" : $"#{TaskNumber}";
}
