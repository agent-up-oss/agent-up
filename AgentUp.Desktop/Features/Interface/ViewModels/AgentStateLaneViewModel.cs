using System.Collections.ObjectModel;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// One branch of the Bridge's fleet graph. All four branches are present even when empty so the
/// graph describes the state model as well as today's counts.
/// </summary>
public sealed class AgentStateLaneViewModel
{
    public AgentStateLaneViewModel(
        TeamStandingKind kind,
        string label,
        string description,
        string glyph,
        IReadOnlyList<TeamStanding> standings)
    {
        Kind = kind;
        Label = label;
        Description = description;
        Glyph = glyph;
        Agents = [.. standings.Select(standing => new StandingRowViewModel(standing))];
    }

    public TeamStandingKind Kind { get; }

    public string Label { get; }

    public string Description { get; }

    public string Glyph { get; }

    public ObservableCollection<StandingRowViewModel> Agents { get; }

    public int Count => Agents.Count;

    public bool IsEmpty => Count == 0;

    public string EmptyLabel => Kind switch
    {
        TeamStandingKind.WaitingOnYou => "No decisions waiting",
        TeamStandingKind.Halted => "No stopped agents",
        TeamStandingKind.Working => "No active work",
        _ => "No agents standing by"
    };

    public string Colour => Kind switch
    {
        TeamStandingKind.WaitingOnYou => AgentUpThemeColors.StatusWarning,
        TeamStandingKind.Halted => AgentUpThemeColors.StatusDanger,
        TeamStandingKind.Working => AgentUpThemeColors.AccentBright,
        _ => AgentUpThemeColors.TextMuted
    };
}
