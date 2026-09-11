using AgentUp.Verification.Features.Verification.Interfaces;

namespace AgentUp.Verification.Tests.Fake;

internal sealed class FakeVerificationClock(DateTimeOffset utcNow) : IVerificationClock
{
    public static FakeVerificationClock AtNoon()
        => new(new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero));

    public DateTimeOffset UtcNow => utcNow;
}
