using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Features.Verification.DTOs;

namespace AgentUp.Server.Features.Diagnostics.Models;

public sealed record WorkspaceReviewSnapshot(
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
    double? CpuPercent,
    long? MemoryBytes,
    long? StorageBytes,
    int? ProcessCount,
    IReadOnlyList<ReviewApplicationSnapshot> Applications,
    IReadOnlyList<DiagnosticEntryDto> Entries,
    IReadOnlyList<ReviewActionSnapshot> RecentActions,
    IReadOnlyList<ReviewScreenshotSnapshot> Screenshots,
    VerificationExportDto? Verification);

public sealed record ReviewApplicationSnapshot(
    string Name,
    string Kind,
    string ProcessState,
    string? Health,
    string Readiness,
    IReadOnlyList<ReviewPortSnapshot> Ports,
    IReadOnlyList<string> Logs,
    bool LogsTruncated);

public sealed record ReviewPortSnapshot(string? Variable, int AllocatedPort, string Protocol);

public sealed record ReviewActionSnapshot(
    string EventId,
    DateTimeOffset Timestamp,
    string Kind,
    string Source,
    string Action,
    string Outcome,
    string Detail);

public sealed record ReviewScreenshotSnapshot(
    string EventId,
    string ArtifactId,
    DateTimeOffset Timestamp,
    string? Url,
    string? MimeType,
    string? ImageBase64);
