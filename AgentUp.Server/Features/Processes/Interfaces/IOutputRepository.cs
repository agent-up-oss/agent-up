namespace AgentUp.Server.Features.Processes.Repositories;

public interface IOutputRepository
{
    Task AppendAsync(string workspaceId, string appName, string line, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAsync(string workspaceId, string appName, CancellationToken ct = default);
    Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default);
}
