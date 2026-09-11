namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// The end-of-run answer to "is every check these changes require proven against the
/// current bytes?".
/// </summary>
/// <param name="Verdicts">One verdict per required check, in stable id order.</param>
/// <param name="Enforcement">Whether unsatisfied verdicts block or only report.</param>
/// <param name="UnmatchedFiles">
/// Changed files no path rule matched. Always blocking: an incomplete map must not read
/// as a pass.
/// </param>
/// <param name="ChangedFileCount">How many changed files the plan considered.</param>
public sealed record GuardReport(
    IReadOnlyList<CheckVerdict> Verdicts,
    VerificationEnforcement Enforcement,
    IReadOnlyList<string> UnmatchedFiles,
    int ChangedFileCount)
{
    public static readonly GuardReport Clean = new([], VerificationEnforcement.Warn, [], 0);

    public IReadOnlyList<CheckVerdict> BlockingVerdicts
        => [.. Verdicts.Where(verdict => verdict.Blocking)];

    public IReadOnlyList<CheckVerdict> SkippedVerdicts
        => [.. Verdicts.Where(verdict => verdict.Kind == CheckVerdictKind.Skipped)];

    public bool Satisfied => BlockingVerdicts.Count == 0 && UnmatchedFiles.Count == 0;

    /// <summary>
    /// Whether the caller should fail. Warn enforcement reports the same verdicts without
    /// failing, which is how a repository completes its path map without blocking work.
    /// </summary>
    public bool ShouldBlock => !Satisfied && Enforcement == VerificationEnforcement.Block;
}
