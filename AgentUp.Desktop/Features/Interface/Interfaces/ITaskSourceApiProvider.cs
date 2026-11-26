using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Interfaces;

/// <summary>What the forge connection and the Tasks page need from the Server.</summary>
public interface ITaskSourceApiProvider
{
    Task<TaskSource?> ReadAsync(CancellationToken cancellationToken);

    Task<TaskSource?> SaveAsync(string workspaceId, string moduleId, string endpointUrl, string token, CancellationToken cancellationToken);

    Task<TaskSource?> ForgetAsync(string workspaceId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskSourceRepository>> ListRepositoriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskSourcePull>> ListPullsAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken);
}
