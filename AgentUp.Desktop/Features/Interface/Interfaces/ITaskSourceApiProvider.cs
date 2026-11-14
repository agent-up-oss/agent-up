using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Interfaces;

/// <summary>What the Tasks Source surface needs from the Server.</summary>
public interface ITaskSourceApiProvider
{
    Task<TaskSource?> ReadAsync(CancellationToken cancellationToken);

    Task<TaskSource?> SetAsync(string moduleId, string owner, string repository, CancellationToken cancellationToken);

    Task<TaskSource?> ClearAsync(CancellationToken cancellationToken);

    /// <summary>Where to send the reader to authorize, or null where no sign-in could be started.</summary>
    Task<string?> StartLoginAsync(CancellationToken cancellationToken);
}
