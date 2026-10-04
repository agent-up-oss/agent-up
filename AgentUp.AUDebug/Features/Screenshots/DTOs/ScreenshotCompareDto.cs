namespace AgentUp.AUDebug.Features.Screenshots.DTOs;

public sealed record ScreenshotCompareDto(bool Match, int DifferingPixels, string Detail);
