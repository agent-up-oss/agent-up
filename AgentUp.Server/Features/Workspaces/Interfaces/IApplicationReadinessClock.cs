namespace AgentUp.Server.Features.Workspaces.Interfaces;

public interface IApplicationReadinessClock
{
    DateTimeOffset UtcNow { get; }

    Task Delay(TimeSpan delay, CancellationToken cancellationToken);
}
