namespace AgentUp.AUDebug.Features.Screens.DTOs;

/// <summary>The record a screens run leaves beside its PNGs, so a reader can audit the whole set.</summary>
public sealed record ScreenRunManifestDto(
    string Server,
    string Workspace,
    IReadOnlyList<ScreenCaptureDto> Screens);
