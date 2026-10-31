using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>
/// The Server's task source as the surface reads and changes it. Nothing is decided here: the
/// Server owns the source and the sign-in, so a change is sent and the answer read back.
/// </summary>
public sealed class TaskSourceService(ITaskSourceApiProvider api)
{
    public Task<TaskSource?> ReadAsync(CancellationToken cancellationToken = default)
        => api.ReadAsync(cancellationToken);

    public Task<TaskSource?> SetAsync(
        string moduleId,
        string owner,
        string repository,
        CancellationToken cancellationToken = default)
        => api.SetAsync(moduleId, owner.Trim(), repository.Trim(), cancellationToken);

    public Task<TaskSource?> ClearAsync(CancellationToken cancellationToken = default)
        => api.ClearAsync(cancellationToken);

    public Task<string?> StartLoginAsync(CancellationToken cancellationToken = default)
        => api.StartLoginAsync(cancellationToken);

    /// <summary>
    /// Splits what a reader pasted into owner and repository.
    ///
    /// People paste `owner/repo`, a browser URL, or a clone URL. All three name the same
    /// repository, so all three are taken rather than only the shape that was asked for.
    /// </summary>
    public static (string Owner, string Repository)? Split(string typed)
    {
        if (string.IsNullOrWhiteSpace(typed)) return null;

        var cleaned = typed.Trim();
        var scheme = cleaned.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            var afterHost = cleaned.IndexOf('/', scheme + 3);
            cleaned = afterHost < 0 ? string.Empty : cleaned[(afterHost + 1)..];
        }

        if (cleaned.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[..^4];

        var parts = cleaned.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length < 2 ? null : (parts[^2], parts[^1]);
    }
}
