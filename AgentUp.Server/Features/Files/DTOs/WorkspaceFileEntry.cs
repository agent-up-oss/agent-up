using AgentUp.Server.Features.Git.DTOs;

namespace AgentUp.Server.Features.Files.DTOs;

/// <summary>
/// A file or folder inside a listed folder. <see cref="Status"/> is the Git change of a file, null
/// when it is unchanged or the workspace is not a repository. <see cref="ChangedFiles"/> counts the
/// changed files under a folder that still exist on disk, so a folded folder still says it holds work.
/// </summary>
public sealed record WorkspaceFileEntry(
    string Name,
    string Path,
    WorkspaceFileEntryKind Kind,
    long? Size,
    GitChangeStatus? Status,
    int ChangedFiles);
