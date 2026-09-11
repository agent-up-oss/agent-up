namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// Why a required check cannot run here. A skipped check is never counted as satisfied.
/// </summary>
public enum CheckSkipReason
{
    None,

    /// <summary>The check declares platforms that do not include this machine.</summary>
    PlatformMismatch,

    /// <summary>The check is marked ciOnly and this is not a CI runner.</summary>
    CiOnly
}
