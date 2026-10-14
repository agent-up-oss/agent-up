namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// One required check as it appears on an export. Status uses the same wire names as the
/// guard: satisfied, neverRun, failed, stale, skipped.
/// </summary>
public sealed record VerificationExportCheck(
    string CheckId,
    string Command,
    string Status,
    string Detail,
    int? ExitCode,
    string? RanAtUtc,
    long? DurationMs,
    string SkipReason);
