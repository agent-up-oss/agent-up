using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Tests.Fake;

internal sealed class AlwaysSupportedInspector : IEnabledAgentInspector
{
    public bool SupportsPermissionRequests(string agentModuleId) => true;
}

internal sealed class EmptyCommitsQueue : ICommitsQueueProvider
{
    public Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
        => Task.FromResult(CommitsQueue.Empty());

    public Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
        => Task.FromResult<string?>(null);

    public Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        => operation(cancellationToken);
}

internal sealed class EmptyCommitsGit : ICommitsGitProvider
{
    public Task<string> GetRepoRootAsync(string worktreePath, CancellationToken cancellationToken = default)
        => Task.FromResult("/repo");

    public Task<IReadOnlyList<string>> GetModifiedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> GetStagedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<IReadOnlyList<string>> GetUntrackedFilesAsync(string worktreePath, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>([]);

    public Task<string> GetDiffAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);

    public Task<bool> HasStagedChangesAsync(string worktreePath, CancellationToken cancellationToken = default)
        => Task.FromResult(false);

    public Task<GitOperationState> GetOperationStateAsync(string worktreePath, CancellationToken cancellationToken = default)
        => Task.FromResult(GitOperationState.None);

    public Task ApplyPatchAsync(string worktreePath, string patch, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RestoreFilesAsync(string worktreePath, IReadOnlyList<string> files, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class ScriptedProcessFactory(Func<ScriptedAcpProcess> create) : AgentUp.Server.Features.Agents.Interfaces.IAgentProcessFactory
{
    public List<ScriptedAcpProcess> Created { get; } = [];

    public IAgentProcessProvider Create()
    {
        var process = create();
        Created.Add(process);
        return process;
    }
}
