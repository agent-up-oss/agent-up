using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// One review slot as it is drawn: a label and a mark. An unfilled slot is still drawn, because
/// "nobody has reviewed this yet" is the thing a reader most needs to see.
/// </summary>
public sealed class ReviewSlotViewModel(ReviewSlot slot)
{
    public string Label => slot.Label;

    public string Glyph => slot.Glyph;

    public string Color => slot.Mark switch
    {
        ReviewMark.Approved => AgentUpThemeColors.AccentBright,
        ReviewMark.Warning => AgentUpThemeColors.StatusWarning,
        ReviewMark.Blocked => AgentUpThemeColors.StatusDanger,
        _ => AgentUpThemeColors.TextFaint
    };

    public string ToolTip => slot.Mark switch
    {
        ReviewMark.Approved => $"{slot.Label} review: approved",
        ReviewMark.Warning => $"{slot.Label} review: findings that do not block",
        ReviewMark.Blocked => $"{slot.Label} review: changes requested",
        _ => $"{slot.Label} review: not given yet"
    };
}
