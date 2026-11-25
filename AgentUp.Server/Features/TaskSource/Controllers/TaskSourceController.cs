using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Services;

namespace AgentUp.Server.Features.TaskSource.Controllers;

/// <summary>The way in to the task source for the surfaces that read and change it.</summary>
public sealed class TaskSourceController(TaskSourceService source)
{
    public TaskSourceDto Describe() => source.Describe();

    public Task<TaskSourceOutcome> SaveAsync(SaveTaskSourceTokenRequest request, CancellationToken cancellationToken)
        => source.SaveAsync(request, cancellationToken);

    public TaskSourceDto Clear(string? workspaceId) => source.Clear(workspaceId);

    public Task<IReadOnlyList<TaskSourceRepositoryDto>> ListRepositoriesAsync(CancellationToken cancellationToken)
        => source.ListRepositoriesAsync(cancellationToken);

    public Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
        => source.ListIssuesAsync(moduleId, owner, repository, cancellationToken);

    public Task<IReadOnlyList<TaskSourcePullDto>> ListPullsAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken)
        => source.ListPullsAsync(moduleId, owner, repository, cancellationToken);
}
