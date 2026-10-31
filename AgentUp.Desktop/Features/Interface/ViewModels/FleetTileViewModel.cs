using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>How much a count matters, which is the only thing that colours a tile.</summary>
public enum FleetTone
{
    Plain,
    Warning,
    Danger
}

/// <summary>
/// One number on the bridge's board, counted across every workspace the Server holds.
///
/// A tile carries no sentence explaining itself: the label names what was counted and the number
/// says how much, which is the whole of what a board is for.
/// </summary>
public sealed class FleetTileViewModel(string label, int count, FleetTone tone = FleetTone.Plain)
{
    public string Label { get; } = label;

    public string Count { get; } = $"{count}";

    /// <summary>
    /// A tile only takes its tone once it has something to report. Zero halted agents is good
    /// news, so colouring that zero red would cry wolf on every quiet Server.
    /// </summary>
    public string Colour { get; } = count == 0 || tone == FleetTone.Plain
        ? AgentUpThemeColors.TextPrimary
        : tone == FleetTone.Danger
            ? AgentUpThemeColors.TextDanger
            : AgentUpThemeColors.TextWarning;
}
