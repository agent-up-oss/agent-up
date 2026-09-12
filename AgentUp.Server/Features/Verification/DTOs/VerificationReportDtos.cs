namespace AgentUp.Server.Features.Verification.DTOs;

/// <summary>One required check as reported over MCP.</summary>
public sealed record VerificationCheckDto(
    string CheckId,
    string Command,
    string Status,
    string Detail,
    IReadOnlyList<string> SelectedBy,
    int CoveredFileCount);

/// <summary>What the current changes require.</summary>
public sealed record VerificationPlanDto(
    int ChangedFileCount,
    IReadOnlyList<string> UnmatchedFiles,
    IReadOnlyList<VerificationCheckDto> Checks);

/// <summary>The end-of-run guard answer.</summary>
public sealed record VerificationGuardDto(
    bool Satisfied,
    bool ShouldBlock,
    string Enforcement,
    int ChangedFileCount,
    IReadOnlyList<string> UnmatchedFiles,
    IReadOnlyList<VerificationCheckDto> Blocking,
    IReadOnlyList<VerificationCheckDto> Skipped,
    IReadOnlyList<VerificationCheckDto> SatisfiedChecks);

/// <summary>The result of executing the required checks.</summary>
public sealed record VerificationRunDto(
    bool Succeeded,
    IReadOnlyList<VerificationOutcomeDto> Outcomes);

/// <summary>One executed check.</summary>
public sealed record VerificationOutcomeDto(
    string CheckId,
    string Command,
    int ExitCode,
    long DurationMs,
    string OutputTail);
