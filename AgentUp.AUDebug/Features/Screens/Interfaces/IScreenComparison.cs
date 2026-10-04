using AgentUp.AUDebug.Features.Screens.DTOs;

namespace AgentUp.AUDebug.Features.Screens.Interfaces;

public interface IScreenComparison
{
    IReadOnlyList<ScreenComparisonDto> Compare(
        IReadOnlyList<ScreenshotSceneCopyDto> documented,
        IReadOnlyList<ScreenCaptureDto> captured);
}
