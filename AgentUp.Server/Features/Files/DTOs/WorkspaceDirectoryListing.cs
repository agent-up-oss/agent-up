namespace AgentUp.Server.Features.Files.DTOs;

/// <summary>
/// One folder of a workspace, one level deep. A client opens a folder by asking for it, so a
/// large repository is never walked further than a reader has unfolded it.
/// </summary>
public sealed record WorkspaceDirectoryListing(
    string WorkspaceId,
    string Path,
    IReadOnlyList<WorkspaceFileEntry> Entries,
    bool Truncated);
