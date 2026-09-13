namespace AgentUp.Browser.Streaming.DTOs;

public sealed record RemoteDisplayViewerOptions(
    string Title,
    string DisplayWebSocketPath,
    string FrameMimeType,
    int Width,
    int Height);
