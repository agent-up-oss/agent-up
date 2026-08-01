using System.Text.Json.Serialization;

namespace AgentUp.Server.Features.Browser.Models;

public sealed record BrowserScreenshotMcpResultDto(
    [property: JsonPropertyName("eventId")] string EventId,
    [property: JsonPropertyName("artifactId")] string ArtifactId,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("mimeType")] string MimeType,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("sizeBytes")] long SizeBytes,
    [property: JsonIgnore] byte[] ImageBytes);
