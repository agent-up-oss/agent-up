namespace AgentUp.AUDebug.Features.Screens.DTOs;

/// <summary>
/// What the design system documents one screen as saying.
/// </summary>
/// <remarks>
/// The Screenshots slice owns the full scene; this is the part the Screens slice needs to
/// compare against a real capture, exchanged across the boundary rather than reaching into it.
/// </remarks>
public sealed record ScreenshotSceneCopyDto(string Id, string Surface, IReadOnlyList<string> Copy);
