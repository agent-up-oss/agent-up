namespace AgentUp.AUDebug.Features.Screens.DTOs;

/// <summary>What a run did with one page-assembly screen.</summary>
public sealed record ScreenCaptureDto(
    string Id,
    string Surface,
    string View,
    string Title,
    string? File,
    string? SkippedReason);
