namespace AgentUp.AUDebug.Features.Screens.DTOs;

/// <summary>How one documented screen compared with the real one that was captured.</summary>
/// <param name="Id">The screen id both sides agree on, such as <c>mobile-git</c>.</param>
/// <param name="Status">What the comparison concluded. See <see cref="ScreenComparisonStatus"/>.</param>
/// <param name="MissingCopy">
/// Copy the design system documents for this screen that the real one did not show. Empty
/// unless <see cref="Status"/> is <see cref="ScreenComparisonStatus.Diverged"/>.
/// </param>
/// <param name="Detail">Why a screen was skipped or could not be compared.</param>
public sealed record ScreenComparisonDto(
    string Id,
    ScreenComparisonStatus Status,
    IReadOnlyList<string> MissingCopy,
    string? Detail);

public enum ScreenComparisonStatus
{
    /// <summary>Every piece of copy the design system documents was on the real screen.</summary>
    Matched,

    /// <summary>The real screen was missing copy the design system documents.</summary>
    Diverged,

    /// <summary>The design system documents this screen but the run captured nothing for it.</summary>
    NotCaptured,

    /// <summary>The run deliberately skipped it, such as a Desktop tab Demo does not build.</summary>
    Skipped,

    /// <summary>Captured, but the surface cannot report its text, so nothing was compared.</summary>
    NotComparable,
}
