namespace AgentUp.Browser.Streaming.Models;

public sealed record BrowserCommandResultDto(
    Guid CommandId,
    bool Success,
    string? Data,
    string? Error);
