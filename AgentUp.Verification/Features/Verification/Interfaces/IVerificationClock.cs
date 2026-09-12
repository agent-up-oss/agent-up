namespace AgentUp.Verification.Features.Verification.Interfaces;

/// <summary>
/// Wall-clock time, injected so receipt timestamps are assertable.
/// </summary>
public interface IVerificationClock
{
    DateTimeOffset UtcNow { get; }
}
