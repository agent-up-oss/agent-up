namespace AgentUp.Server.Features.Diagnostics.DTOs;

public sealed record DiagnosticEntryDto(
    string Id,
    DateTimeOffset Timestamp,
    string Category,
    string Severity,
    string State,
    string Source,
    string Action,
    string? Application,
    string? BrowserSession,
    string Message,
    IReadOnlyDictionary<string, string> Details);
