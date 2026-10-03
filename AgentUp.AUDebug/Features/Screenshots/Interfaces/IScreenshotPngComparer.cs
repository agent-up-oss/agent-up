using AgentUp.AUDebug.Features.Screenshots.DTOs;

namespace AgentUp.AUDebug.Features.Screenshots.Interfaces;

public interface IScreenshotPngComparer
{
    ScreenshotCompareDto Compare(string expectedPath, string actualPath);
}
