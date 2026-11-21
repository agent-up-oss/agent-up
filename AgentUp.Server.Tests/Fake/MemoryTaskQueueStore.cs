using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Tests.Fake;

/// <summary>A task queue store that keeps the queue in memory, as the JSON store would on disk.</summary>
internal sealed class MemoryTaskQueueStore : ITaskQueueStore
{
    private IReadOnlyList<QueuedTask> _tasks = [];

    public IReadOnlyList<QueuedTask> Read() => _tasks;

    public void Write(IReadOnlyList<QueuedTask> tasks) => _tasks = tasks;
}
