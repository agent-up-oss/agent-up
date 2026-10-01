using AgentUp.AUDebug.Features.Screenshots.DTOs;

namespace AgentUp.AUDebug.Features.Screenshots.Interfaces;

public interface IScreenshotMediaStore
{
    string HtmlPath(ScreenshotSceneDto scene);
    string MediaPath(string mediaFile);
    string HeroPath();
    string StagingPath(string fileName);
    string CapturePath(string sceneId);
    string FileUrl(string path);
    bool Exists(string path);
    void Copy(string source, string destination);
    void EnsureParent(string path);
}
