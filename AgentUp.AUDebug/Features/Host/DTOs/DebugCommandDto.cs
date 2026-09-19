namespace AgentUp.AUDebug.Features.Host.DTOs;

public sealed record DebugCommandDto(
    string Verb,
    string? Surface,
    string? Action,
    string? WorkspaceName,
    string? Password,
    TimeSpan Timeout,
    bool Detach,
    string? Suite = null,
    string? PagePath = null,
    string? Heading = null,
    bool FullPage = false);
