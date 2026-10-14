namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>Delays a loop retry after a rate-limit signal. Unit tests inject a no-op.</summary>
public interface ILoopBackoff
{
    Task DelayAsync(int attempt, CancellationToken cancellationToken);
}
