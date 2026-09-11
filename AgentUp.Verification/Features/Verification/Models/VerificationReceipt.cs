namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// Durable proof that one check ran to completion against a specific set of file contents.
/// A receipt is only valid for the exact bytes recorded in <paramref name="Covered"/>, so
/// editing a covered file after the run makes the receipt stale rather than satisfying.
/// </summary>
/// <param name="CheckId">The check this receipt proves.</param>
/// <param name="Command">The command that was executed, recorded so a changed command invalidates the proof.</param>
/// <param name="ExitCode">Process exit code. Only zero can satisfy a guard.</param>
/// <param name="RanAtUtc">Round-trip formatted UTC timestamp, for reporting only.</param>
/// <param name="DurationMs">Wall-clock duration, for reporting only.</param>
/// <param name="Covered">Repo-relative path to content hash, as of the moment the check ran.</param>
public sealed record VerificationReceipt(
    string CheckId,
    string Command,
    int ExitCode,
    string RanAtUtc,
    long DurationMs,
    IReadOnlyDictionary<string, string> Covered)
{
    public bool Succeeded => ExitCode == 0;
}
