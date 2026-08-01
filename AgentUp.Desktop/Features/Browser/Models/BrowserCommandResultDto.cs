namespace AgentUp.Desktop.Features.Browser.Models;

internal sealed record BrowserCommandResultDto(
    Guid CommandId,
    bool Success,
    string? Data,
    string? Error);
