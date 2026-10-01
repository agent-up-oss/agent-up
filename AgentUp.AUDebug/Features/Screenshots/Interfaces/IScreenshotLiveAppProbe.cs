namespace AgentUp.AUDebug.Features.Screenshots.Interfaces;

public interface IScreenshotLiveAppProbe
{
    Task<string> ReadPageTextAsync(string url, CancellationToken cancellationToken);
}
