using AgentUp.Server.Features.Files.Models;

namespace AgentUp.Server.Features.Files.Interfaces;

/// <summary>
/// The workspace folder on the Server's disk. Every path is relative to <c>root</c>, and a path
/// that leaves it - through <c>..</c>, an absolute path, a link, or the repository's own
/// <c>.git</c> - is refused with <see cref="WorkspaceFilePathException"/>.
/// </summary>
public interface IWorkspaceFileSystemProvider
{
    /// <summary>The folder's direct entries, or null when it is not a folder.</summary>
    WorkspaceDiskListing? ListDirectory(string root, string path, int maxEntries);

    /// <summary>The file, or null when it is not a file.</summary>
    Task<WorkspaceDiskFile?> ReadFileAsync(string root, string path, long maxBytes, CancellationToken cancellationToken);

    /// <summary>Replaces an existing file's bytes in place.</summary>
    Task WriteFileAsync(string root, string path, byte[] bytes, CancellationToken cancellationToken);
}
