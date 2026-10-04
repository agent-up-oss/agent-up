namespace AgentUp.AUDebug.Features.Screens.DTOs;

/// <summary>What a run did with one page-assembly screen.</summary>
/// <param name="Text">
/// The text the real screen was showing, where the surface can report it. <c>screens compare</c>
/// holds the design system's documented copy against this.
/// </param>
public sealed record ScreenCaptureDto(
    string Id,
    string Surface,
    string View,
    string Title,
    string? File,
    string? SkippedReason,
    string? Text = null);
