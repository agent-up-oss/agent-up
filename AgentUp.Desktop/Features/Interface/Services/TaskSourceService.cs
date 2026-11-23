using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>
/// The Server's forge connections as the surface reads and changes them. A token is sent and
/// never kept here: the Server checks it and stores it.
/// </summary>
public sealed class TaskSourceService(ITaskSourceApiProvider api)
{
    public Task<TaskSource?> ReadAsync(CancellationToken cancellationToken = default)
        => api.ReadAsync(cancellationToken);

    public Task<TaskSource?> SaveAsync(string moduleId, string token, CancellationToken cancellationToken = default)
        => api.SaveAsync(moduleId, token.Trim(), cancellationToken);

    public Task<TaskSource?> ForgetAsync(string moduleId, CancellationToken cancellationToken = default)
        => api.ForgetAsync(moduleId, cancellationToken);

    public Task<IReadOnlyList<TaskSourceRepository>> ListRepositoriesAsync(CancellationToken cancellationToken = default)
        => api.ListRepositoriesAsync(cancellationToken);

    public Task<IReadOnlyList<TaskSourcePull>> ListPullsAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
        => api.ListPullsAsync(moduleId, owner, repository, cancellationToken);
}
