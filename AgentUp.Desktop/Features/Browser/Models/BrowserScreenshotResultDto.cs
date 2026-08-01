namespace AgentUp.Desktop.Features.Browser.Models;

internal sealed record BrowserScreenshotResultDto(
    string Url,
    string MimeType,
    string ImageBase64,
    int Width,
    int Height);
