namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>Renders an HTML file to a PNG. The summary page is what gets captured, not the long report.</summary>
public interface IReportPageCapture
{
    Task CaptureAsync(
        string htmlPath,
        string pngPath,
        int width,
        int height,
        CancellationToken cancellationToken);
}
