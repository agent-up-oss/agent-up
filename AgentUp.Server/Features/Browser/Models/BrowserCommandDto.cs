namespace AgentUp.Server.Features.Browser.Models;

public sealed record BrowserCommandDto(
    Guid CommandId,
    string WorkspaceId,
    BrowserCommandKind Kind,
    string? Url,
    string? Selector,
    string? Text,
    string? Key,
    int TimeoutMs,
    bool ReloadIfSameUrl = true);
