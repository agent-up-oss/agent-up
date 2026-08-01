namespace AgentUp.Server.Features.Browser.Models;

public sealed record BrowserScreenshotResultDto(
    string Url,
    string MimeType,
    string ImageBase64,
    int Width,
    int Height);
