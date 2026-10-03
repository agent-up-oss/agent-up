using AgentUp.AUDebug.Features.Screenshots.DTOs;

namespace AgentUp.AUDebug.Features.Screenshots.Interfaces;

public interface IScreenshotAppContract
{
    void Verify(ScreenshotSceneDto scene);
}
