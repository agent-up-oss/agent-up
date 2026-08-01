namespace AgentUp.Server.Features.Browser.Models;

public sealed record BrowserCommandResultDto(
    Guid CommandId,
    bool Success,
    string? Data,
    string? Error);
