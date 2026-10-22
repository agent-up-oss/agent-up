using AgentUp.Server.Features.Workspaces.Interfaces;

namespace AgentUp.Server.Features.Workspaces.Providers;

public sealed class ApplicationReadinessClock : IApplicationReadinessClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public Task Delay(TimeSpan delay, CancellationToken cancellationToken)
        => Task.Delay(delay, cancellationToken);
}
