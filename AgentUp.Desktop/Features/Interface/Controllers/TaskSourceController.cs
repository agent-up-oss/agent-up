using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;

namespace AgentUp.Desktop.Features.Interface.Controllers;

/// <summary>The way in to the Server's task source for the surface that shows it.</summary>
public sealed class TaskSourceController(TaskSourceService source)
{
    public Task<TaskSource?> ReadAsync(CancellationToken cancellationToken = default)
        => source.ReadAsync(cancellationToken);

    public Task<TaskSource?> SetAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
        => source.SetAsync(moduleId, owner, repository, cancellationToken);

    public Task<TaskSource?> ClearAsync(CancellationToken cancellationToken = default)
        => source.ClearAsync(cancellationToken);

    public Task<string?> StartLoginAsync(CancellationToken cancellationToken = default)
        => source.StartLoginAsync(cancellationToken);
}
