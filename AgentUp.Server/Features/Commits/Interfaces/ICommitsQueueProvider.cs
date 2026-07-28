using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Features.Commits.Interfaces;

public interface ICommitsQueueProvider
{
    Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default);
    Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default);
    Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default);
    Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default);
    Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default);
    Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
