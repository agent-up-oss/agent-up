namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// The result of executing one planned check.
/// </summary>
public sealed record CheckOutcome(
    string CheckId,
    string Command,
    int ExitCode,
    long DurationMs,
    string Output)
{
    public bool Succeeded => ExitCode == 0;
}
