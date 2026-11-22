namespace AgentUp.Desktop.Features.Files.DTOs;

/// <summary>A file or folder in one listed folder, as the Server's Files slice reports it.</summary>
public sealed record WorkspaceFileEntryDto(
    string Name,
    string Path,
    string Kind,
    long? Size,
    string? Status,
    int ChangedFiles)
{
    public bool IsDirectory => string.Equals(Kind, "Directory", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One folder, one level deep. The Server cuts a very large folder and says so.</summary>
public sealed record WorkspaceDirectoryListingDto(
    string WorkspaceId,
    string Path,
    IReadOnlyList<WorkspaceFileEntryDto> Entries,
    bool Truncated);

/// <summary>A whole file. Content is null for a binary file or one over the size the Server sends.</summary>
public sealed record WorkspaceFileContentDto(
    string Path,
    long Size,
    string Version,
    string? Status,
    bool IsBinary,
    bool IsTooLarge,
    string? Content);

public sealed record WorkspaceFileWriteRequestDto(string Path, string Content, string Version);

/// <summary>
/// What a save came to: the saved file, or why it was not saved. <see cref="Conflict"/> means the
/// file changed on the Server since it was opened, which only a reload resolves.
/// </summary>
public sealed record WorkspaceFileSaveResultDto(WorkspaceFileContentDto? Saved, bool Conflict, string? Error)
{
    public static WorkspaceFileSaveResultDto Success(WorkspaceFileContentDto saved) => new(saved, false, null);

    public static WorkspaceFileSaveResultDto Failed(string error, bool conflict = false) => new(null, conflict, error);
}
