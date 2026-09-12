namespace AgentUp.Desktop.Features.Git.DTOs;

public sealed record GitChangeTreeDto(
    string WorkspaceId,
    string Branch,
    int FileCount,
    GitChangeDirectoryDto Root,
    IReadOnlyList<string>? LocalBranches = null);

public sealed record GitChangeDirectoryDto(
    string Name,
    string Path,
    IReadOnlyList<GitChangeDirectoryDto> Directories,
    IReadOnlyList<GitChangeFileDto> Files);

public sealed record GitChangeFileDto(
    string Name,
    string Path,
    string Status);
