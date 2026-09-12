namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// The guard's finding for one required check.
/// </summary>
public enum CheckVerdictKind
{
    /// <summary>A successful receipt matches the current covered map exactly.</summary>
    Satisfied,

    /// <summary>No receipt exists for this check.</summary>
    NeverRun,

    /// <summary>A receipt exists but the check exited non-zero.</summary>
    Failed,

    /// <summary>A successful receipt exists but the covered content changed after it ran.</summary>
    Stale,

    /// <summary>The check cannot run on this machine. Not satisfied, but not blocking here.</summary>
    Skipped
}
