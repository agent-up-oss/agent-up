using AgentUp.Desktop.Features.Files.DTOs;
using AgentUp.Desktop.Features.Files.Interfaces;

namespace AgentUp.Desktop.Features.Files.Services;

/// <summary>
/// The Files tab's reads and saves. The Server owns the clone; Desktop holds only what is on screen,
/// so a folder is asked for when it is unfolded and a file when it is opened.
/// </summary>
public sealed class WorkspaceFilesService(IWorkspaceFilesApiProvider client)
{
    public Task<WorkspaceDirectoryListingDto?> GetDirectoryAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
        => client.GetDirectoryAsync(workspaceId, path, cancellationToken);

    public Task<WorkspaceFileContentDto?> GetFileAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
        => client.GetFileAsync(workspaceId, path, cancellationToken);

    /// <summary>Saves an edit made from <paramref name="version"/>; the Server refuses it if the file moved on since.</summary>
    public Task<WorkspaceFileSaveResultDto> SaveAsync(string workspaceId, string path, string content, string version, CancellationToken cancellationToken = default)
        => client.SaveFileAsync(workspaceId, new WorkspaceFileWriteRequestDto(path, content, version), cancellationToken);
}
