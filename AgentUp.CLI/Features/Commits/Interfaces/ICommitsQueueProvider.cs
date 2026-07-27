using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.Interfaces;

public interface ICommitsQueueProvider
{
    Task<CommitsQueue> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(CommitsQueue queue, CancellationToken cancellationToken = default);
    Task DeleteAsync(CancellationToken cancellationToken = default);
    Task SavePatchAsync(string patchKey, string patch, CancellationToken cancellationToken = default);
    Task<string?> ReadPatchAsync(string patchKey, CancellationToken cancellationToken = default);
    Task<T> WithLockAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
