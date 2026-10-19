namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// One verification run, written for a program. The HTML and PNG views are rendered from this.
/// </summary>
public sealed record RunReport(
    string Version,
    string ConfigPath,
    string ConfigHash,
    string Commit,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<RunReportCheck> Checks,
    int Ran,
    int Passed,
    int Failed,
    int CouldNotRun,
    int ExitCode,
    string ExitReason);

/// <summary>One check inside a <see cref="RunReport"/>.</summary>
public sealed record RunReportCheck(
    string Id,
    string Tier,
    string Command,
    string Outcome,
    long? DurationMs,
    string? SkipReason,
    string? MissingToolchain,
    string? Output);
