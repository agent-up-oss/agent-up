namespace AgentUp.CLI.Features.Workspaces.DTOs;

public sealed record WorkspaceDiagnosticsDto(
    string WorkspaceId,
    string WorkspaceName,
    string ProcessState,
    string? Health,
    DateTimeOffset CapturedAt,
    IReadOnlyList<ApplicationDiagnosticsDto> Applications,
    IReadOnlyList<DiagnosticEntryDto> Entries);

public sealed record ApplicationDiagnosticsDto(
    string Name,
    string ProcessState,
    string? Health,
    IReadOnlyList<string> Logs,
    bool LogsTruncated);

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
