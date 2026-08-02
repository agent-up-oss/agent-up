namespace AgentUp.Server.Features.Orchestration.DTOs;

public sealed record WorkspaceConsoleSnapshot(
    string WorkspaceId,
    string DisplayName,
    DateTimeOffset CapturedAt,
    IReadOnlyList<ApplicationConsoleSnapshot> Applications,
    IReadOnlyList<WorkspaceConsoleAuditEvent> AuditTrail);

public sealed record ApplicationConsoleSnapshot(
    string ApplicationName,
    string State,
    int TotalLineCount,
    bool Truncated,
    IReadOnlyList<string> Lines);

public sealed record WorkspaceConsoleAuditEvent(
    string EventId,
    DateTimeOffset Timestamp,
    string Kind,
    string Source,
    string Action,
    string Outcome,
    string? ApplicationName,
    string? Stream,
    string? Message);
