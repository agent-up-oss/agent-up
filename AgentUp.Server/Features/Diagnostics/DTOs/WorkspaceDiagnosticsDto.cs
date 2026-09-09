namespace AgentUp.Server.Features.Diagnostics.DTOs;

public sealed record WorkspaceDiagnosticsDto(
    string WorkspaceId,
    string WorkspaceName,
    string ProcessState,
    string? Health,
    DateTimeOffset CapturedAt,
    IReadOnlyList<ApplicationDiagnosticsDto> Applications,
    IReadOnlyList<DiagnosticEntryDto> Entries);
