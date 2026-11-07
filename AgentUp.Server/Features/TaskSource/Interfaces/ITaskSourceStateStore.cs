namespace AgentUp.Server.Features.TaskSource.Interfaces;

/// <summary>The one-time value that ties an authorize redirect back to the request that began it.</summary>
public interface ITaskSourceStateStore
{
    string Issue(string moduleId);

    string? Consume(string state);
}
