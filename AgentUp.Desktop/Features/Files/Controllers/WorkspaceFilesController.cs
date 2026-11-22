using AgentUp.Desktop.Features.Files.DTOs;
using AgentUp.Desktop.Features.Files.Services;

namespace AgentUp.Desktop.Features.Files.Controllers;

public sealed class WorkspaceFilesController(WorkspaceFilesService files)
{
    public Task<WorkspaceDirectoryListingDto?> GetDirectoryAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
        => files.GetDirectoryAsync(workspaceId, path, cancellationToken);

    public Task<WorkspaceFileContentDto?> GetFileAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
        => files.GetFileAsync(workspaceId, path, cancellationToken);

    public Task<WorkspaceFileSaveResultDto> SaveAsync(string workspaceId, string path, string content, string version, CancellationToken cancellationToken = default)
        => files.SaveAsync(workspaceId, path, content, version, cancellationToken);
}
