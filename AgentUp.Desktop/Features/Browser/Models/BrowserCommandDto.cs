namespace AgentUp.Desktop.Features.Browser.Models;

internal sealed record BrowserCommandDto(
    Guid CommandId,
    string WorkspaceId,
    BrowserCommandKind Kind,
    string? Url,
    string? Selector,
    string? Text,
    string? Key,
    int TimeoutMs);
