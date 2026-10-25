using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Interfaces;

/// <summary>
/// Where the picked queue survives a restart. The order of the list is the order of the queue.
/// </summary>
public interface ITaskQueueStore
{
    IReadOnlyList<QueuedTask> Read();

    void Write(IReadOnlyList<QueuedTask> tasks);
}
