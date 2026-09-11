namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// How expensive a check is, and therefore how broadly it can be required.
/// </summary>
public enum CheckTier
{
    /// <summary>Seconds. Safe to require from many path rules.</summary>
    Fast,

    /// <summary>Minutes. Require only from the paths that genuinely need it.</summary>
    Slow,

    /// <summary>Needs a packaged artifact or a specific operating system.</summary>
    Platform
}
