using AgentUp.Server.Features.Agents.Interfaces;

namespace AgentUp.Server.Tests.Fake;

internal sealed class ImmediateLoopBackoff : ILoopBackoff
{
    public List<int> Attempts { get; } = [];

    public Task DelayAsync(int attempt, CancellationToken cancellationToken)
    {
        Attempts.Add(attempt);
        return Task.CompletedTask;
    }
}
