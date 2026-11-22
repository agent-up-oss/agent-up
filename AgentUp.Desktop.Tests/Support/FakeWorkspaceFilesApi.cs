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

    /// <summary>When set, every call fails the way an unreachable Server does.</summary>
    public string? Failure { get; set; }

    /// <summary>Requests named here wait until the test completes them, so a later request can overtake.</summary>
    public Dictionary<string, TaskCompletionSource> Held { get; } = new(StringComparer.Ordinal);

    public TaskCompletionSource Hold(string key)
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Held[key] = gate;
        return gate;
    }

    private async Task WaitAsync(string key)
    {
        if (Held.Remove(key, out var gate))
            await gate.Task;
        if (Failure is not null)
            throw new HttpRequestException(Failure);
    }

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

    public async Task<WorkspaceDirectoryListingDto?> GetDirectoryAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
    {
        Requested.Add(path);
        await WaitAsync($"dir:{path}");
        return Folders.GetValueOrDefault(path);
    }

    public async Task<WorkspaceFileContentDto?> GetFileAsync(string workspaceId, string path, CancellationToken cancellationToken = default)
    {
        await WaitAsync($"file:{path}");
        return Files.GetValueOrDefault(path);
    }

    public async Task<WorkspaceFileSaveResultDto> SaveFileAsync(string workspaceId, WorkspaceFileWriteRequestDto request, CancellationToken cancellationToken = default)
    {
        await WaitAsync($"save:{request.Path}");
        Saved.Add(request);
        var result = NextSave ?? WorkspaceFileSaveResultDto.Success(
            new WorkspaceFileContentDto(request.Path, request.Content.Length, $"v-{request.Content}", "Modified", false, false, request.Content));
        NextSave = null;
        return result;
    }
}
