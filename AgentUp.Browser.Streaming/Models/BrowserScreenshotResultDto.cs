namespace AgentUp.Browser.Streaming.Models;

public sealed record BrowserScreenshotResultDto(
    string Url,
    string MimeType,
    string ImageBase64,
    int Width,
    int Height);
