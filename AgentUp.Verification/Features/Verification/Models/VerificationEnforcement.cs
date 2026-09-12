namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>
/// Whether an unsatisfied guard reports or blocks. Start at <see cref="Warn"/> while a
/// repository's path map is still being completed, then switch to <see cref="Block"/>.
/// </summary>
public enum VerificationEnforcement
{
    Warn,
    Block
}
