using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Services;

namespace AgentUp.Server.Features.TaskSource.Controllers;

/// <summary>The way in to the task source for the surfaces that read and change it.</summary>
public sealed class TaskSourceController(TaskSourceService source)
{
    public TaskSourceDto Describe() => source.Describe();

    public TaskSourceDto Set(SetTaskSourceRequest request) => source.Set(request);

    public TaskSourceDto Clear() => source.Clear();

    public TaskSourceOutcome StartLogin() => source.StartLogin();

    public Task<bool> CompleteLoginAsync(string code, string state, CancellationToken cancellationToken)
        => source.CompleteLoginAsync(code, state, cancellationToken);

    public Task<IReadOnlyList<ForgeIssue>> ListIssuesAsync(CancellationToken cancellationToken)
        => source.ListIssuesAsync(cancellationToken);
}
