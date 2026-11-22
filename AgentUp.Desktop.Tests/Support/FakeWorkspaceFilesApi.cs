using AgentUp.Desktop.Features.Files.DTOs;
using AgentUp.Desktop.Features.Files.Interfaces;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>The Files API over a dictionary of folders, for view-model tests that need no HTTP.</summary>
internal sealed class FakeWorkspaceFilesApi : IWorkspaceFilesApiProvider
{
    public Dictionary<string, WorkspaceDirectoryListingDto> Folders { get; } = new(StringComparer.Ordinal);

    public Dictionary<string, WorkspaceFileContentDto> Files { get; } = new(StringComparer.Ordinal);

    public List<string> Requested { get; } = [];

    public WorkspaceFileSaveResultDto? NextSave { get; set; }

    public List<WorkspaceFileWriteRequestDto> Saved { get; } = [];

    public FakeWorkspaceFilesApi Folder(string path, params WorkspaceFileEntryDto[] entries)
    {
        Folders[path] = new WorkspaceDirectoryListingDto(DesktopDomain.WorkspaceId, path, entries, false);
        return this;
    }

    public FakeWorkspaceFilesApi File(string path, string? content, string? status = null, bool isBinary = false)
    {
        Files[path] = new WorkspaceFileContentDto(path, content?.Length ?? 4096, $"v-{content}", status, isBinary, false, content);
        return this;
    }

    public static WorkspaceFileEntryDto Dir(string path, int changed = 0)
        => new(Path.GetFileName(path), path, "Directory", null, null, changed);

    public static WorkspaceFileEntryDto Leaf(string path, string? status = null, long size = 12)
        => new(Path.GetFileName(path), path, "File", size, status, 0);

    public Task<WorkspaceDirectoryListingDto?> GetDirectoryAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
    {
        Requested.Add(path);
        return Task.FromResult(Folders.GetValueOrDefault(path));
    }

    public Task<WorkspaceFileContentDto?> GetFileAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
        => Task.FromResult(Files.GetValueOrDefault(path));

    public Task<WorkspaceFileSaveResultDto> SaveFileAsync(string workspaceId, WorkspaceFileWriteRequestDto request, CancellationToken cancellationToken = default)
    {
        Saved.Add(request);
        var result = NextSave ?? WorkspaceFileSaveResultDto.Success(
            new WorkspaceFileContentDto(request.Path, request.Content.Length, $"v-{request.Content}", "Modified", false, false, request.Content));
        NextSave = null;
        return Task.FromResult(result);
    }
}
