using AgentUp.Desktop.Features.Files.DTOs;

namespace AgentUp.Desktop.Features.Files.Interfaces;

public interface IWorkspaceFilesApiProvider
{
    /// <summary>One folder of the workspace, or null when the workspace or folder is gone.</summary>
    Task<WorkspaceDirectoryListingDto?> GetDirectoryAsync(string workspaceId, string path, CancellationToken cancellationToken = default);

    /// <summary>One whole file, or null when the workspace or file is gone.</summary>
    Task<WorkspaceFileContentDto?> GetFileAsync(string workspaceId, string path, CancellationToken cancellationToken = default);

    Task<WorkspaceFileSaveResultDto> SaveFileAsync(string workspaceId, WorkspaceFileWriteRequestDto request, CancellationToken cancellationToken = default);
}
