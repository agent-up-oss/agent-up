namespace AgentUp.AUDebug.Features.Screenshots.DTOs;

public sealed class ScreenshotManifestDto
{
    public IReadOnlyList<ScreenshotSceneDto> Scenes { get; init; } = [];
}
