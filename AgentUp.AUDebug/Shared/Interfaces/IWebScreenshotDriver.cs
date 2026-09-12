namespace AgentUp.AUDebug.Shared.Interfaces;

public interface IWebScreenshotDriver
{
    Task CaptureAsync(string url, string outputPath, CancellationToken cancellationToken);
}
