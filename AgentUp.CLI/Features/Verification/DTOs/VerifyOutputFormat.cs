namespace AgentUp.CLI.Features.Verification.DTOs;

/// <summary>
/// How <c>agentup verify</c> renders its result.
/// </summary>
public enum VerifyOutputFormat
{
    /// <summary>Readable output for a developer at a terminal.</summary>
    Text,

    /// <summary>
    /// Terse output for a Stop hook: silent on success, blockers on stderr otherwise, so
    /// a passing turn prints nothing and a failing one is fed back to the agent.
    /// </summary>
    Hook
}
