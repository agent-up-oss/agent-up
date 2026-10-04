namespace AgentUp.AUDebug.Shared.Interfaces;

public interface IWebScreenshotDriver
{
    Task CaptureAsync(
        string url,
        string outputPath,
        CancellationToken cancellationToken,
        string? userDataDirectory = null,
        int width = 1440,
        int height = 900);
}
