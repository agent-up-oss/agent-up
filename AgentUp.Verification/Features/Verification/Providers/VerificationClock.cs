using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Features.Verification.Providers;

public sealed class VerificationClock : IVerificationClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
