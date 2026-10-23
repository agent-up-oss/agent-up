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

    public string Subtitle => Standing.Agent is { Length: > 0 } agent
        ? $"{agent} · {Standing.WorkspaceState}"
        : Standing.WorkspaceState;

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

    public string Headline => Standing.Kind switch
    {
        TeamStandingKind.WaitingOnYou => $"{DisplayName} is waiting on you{TaskSuffix}",
        TeamStandingKind.Halted => $"{DisplayName} stopped{TaskSuffix}",
        TeamStandingKind.Working => $"{DisplayName} is working{TaskSuffix}",
        _ => $"{DisplayName} has nothing in hand"
    };

    public string? Reason => Standing.Reason;

    public bool HasReason => Standing.Reason is { Length: > 0 };

    public string? DiagnosticCode => Standing.DiagnosticCode;

    public bool HasDiagnosticCode => Standing.DiagnosticCode is { Length: > 0 };

    /// <summary>The permissions a gate is waiting on, as the Server named them.</summary>
    public string PermissionSummary => Standing.PermissionRuleIds.Count == 0
        ? string.Empty
        : $"needs {string.Join(", ", Standing.PermissionRuleIds)}";

    public bool HasPermissionSummary => Standing.PermissionRuleIds.Count > 0;

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

    /// <summary>The checks as counts, which is all a triage row needs to show.</summary>
    public string ChecksSummary => Parts().Count == 0 ? "no checks reported" : string.Join(" · ", Parts());

    public string ForgeStatus => Standing.Forge.Availability switch
    {
        "ready" => Standing.Forge.MergeState is { Length: > 0 } state ? $"mergeable: {state}" : "host connected",
        "noProposal" => "no proposal open",
        "needsCredential" => "host needs a credential",
        "unreachable" => "host unreachable",
        _ => "no host connected"
    };

    public bool HasUnresolvedThreads => Standing.Forge.UnresolvedThreads > 0;

    public string ThreadSummary => Standing.Forge.UnresolvedThreads == 1
        ? "1 unresolved review thread"
        : $"{Standing.Forge.UnresolvedThreads} unresolved review threads";

    private string TaskSuffix => Standing.TaskNumber is null ? string.Empty : $" on #{Standing.TaskNumber}";

    private List<string> Parts()
    {
        var parts = new List<string>();
        if (Standing.Forge.ChecksPassed > 0) parts.Add($"{Standing.Forge.ChecksPassed} passing");
        if (Standing.Forge.ChecksFailed > 0) parts.Add($"{Standing.Forge.ChecksFailed} failing");
        if (Standing.Forge.ChecksRunning > 0) parts.Add($"{Standing.Forge.ChecksRunning} running");
        return parts;
    }
}
