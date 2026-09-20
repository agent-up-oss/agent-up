namespace AgentUp.AUDebug.Features.Docs.Interfaces;

public interface IDocsPageCapture
{
    Task CaptureAsync(
        string url,
        string outputPath,
        string? heading,
        bool fullPage,
        CancellationToken cancellationToken);
}
