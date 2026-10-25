using AgentUp.Server.Features.Verification.DTOs;

namespace AgentUp.Server.Features.Diagnostics.DTOs;

public sealed record WorkspaceReviewSnapshotDto(
    string WorkspaceId,
    string DisplayName,
    string RepositoryPath,
    string WorktreePath,
    string Branch,
    string Commit,
    string State,
    string? Health,
    DateTimeOffset CapturedAt,
    string? BrowserUrl,
    IReadOnlyList<ReviewApplicationSnapshotDto> Applications,
    IReadOnlyList<DiagnosticEntryDto> Entries,
    IReadOnlyList<ReviewActionSnapshotDto> RecentActions,
    IReadOnlyList<ReviewScreenshotSnapshotDto> Screenshots,
    VerificationExportDto? Verification);

public sealed record ReviewApplicationSnapshotDto(
    string Name,
    string Kind,
    string ProcessState,
    string? Health,
    IReadOnlyList<ReviewPortSnapshotDto> Ports,
    IReadOnlyList<string> Logs,
    bool LogsTruncated);

public sealed record ReviewPortSnapshotDto(string? Variable, int AllocatedPort, string Protocol);

public sealed record ReviewActionSnapshotDto(
    string EventId,
    DateTimeOffset Timestamp,
    string Kind,
    string Source,
    string Action,
    string Outcome);

public sealed record ReviewScreenshotSnapshotDto(
    string EventId,
    string ArtifactId,
    DateTimeOffset Timestamp,
    string? Url,
    string? MimeType);
