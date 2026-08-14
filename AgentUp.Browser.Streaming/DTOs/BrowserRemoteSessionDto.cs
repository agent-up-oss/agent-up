namespace AgentUp.Browser.Streaming.DTOs;

public sealed record BrowserRemoteSessionDto(
    string WorkspaceId,
    string Transport,
    string ViewerPath,
    string DisplayWebSocketPath,
    string LatestFramePath,
    string ControlAuthority,
    int Width,
    int Height,
    IReadOnlyList<BrowserViewportPresetDto> ViewportPresets,
    string SelectedPresetId,
    bool TouchCapable);

public sealed record BrowserViewportPresetDto(string Id, string Label, int Width, int Height);
