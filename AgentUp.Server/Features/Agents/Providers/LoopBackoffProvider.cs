using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>Exponential delay after a rate-limit signal. The initial wait is 250 ms, capped at 8 s.</summary>
public sealed class LoopBackoffProvider(int initialMilliseconds = 250, int capMilliseconds = 8_000) : ILoopBackoff
{
    public Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        var shift = Math.Clamp(attempt - 1, 0, 8);
        var milliseconds = Math.Min(capMilliseconds, Math.Max(1, initialMilliseconds) * (1 << shift));
        return Task.Delay(milliseconds, cancellationToken);
    }
}
