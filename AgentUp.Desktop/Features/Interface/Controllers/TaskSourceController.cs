using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;

namespace AgentUp.Desktop.Features.Interface.Controllers;

/// <summary>The way in to the Server's forge connections for the surface that shows them.</summary>
public sealed class TaskSourceController(TaskSourceService source)
{
    public Task<TaskSource?> ReadAsync(CancellationToken cancellationToken = default)
        => source.ReadAsync(cancellationToken);

    public Task<TaskSource?> SaveAsync(string moduleId, string token, CancellationToken cancellationToken = default)
        => source.SaveAsync(moduleId, token, cancellationToken);

    public Task<TaskSource?> ForgetAsync(string moduleId, CancellationToken cancellationToken = default)
        => source.ForgetAsync(moduleId, cancellationToken);

    public Task<IReadOnlyList<TaskSourceRepository>> ListRepositoriesAsync(CancellationToken cancellationToken = default)
        => source.ListRepositoriesAsync(cancellationToken);

    public Task<IReadOnlyList<TaskSourcePull>> ListPullsAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
        => source.ListPullsAsync(moduleId, owner, repository, cancellationToken);
}
