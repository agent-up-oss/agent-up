using AgentUp.Server.Features.Processes.Models;

namespace AgentUp.Server.Features.Processes.Interfaces;

public interface IOutputRepository
{
    Task AppendAsync(string workspaceId, string appName, ProcessOutputLine line, CancellationToken ct = default);
    Task<IReadOnlyList<ProcessOutputLine>> GetAsync(string workspaceId, string appName, CancellationToken ct = default);
    Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default);
}
