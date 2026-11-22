using AgentUp.Server.Features.Git.DTOs;

namespace AgentUp.Server.Features.Files.DTOs;

/// <summary>
/// A whole file as it is on the Server. <see cref="Version"/> names these exact bytes, and a save
/// must name the version it was edited from. <see cref="Content"/> is null for a binary file or one
/// over the size the Server hands to clients; neither is editable.
/// </summary>
public sealed record WorkspaceFileContent(
    string Path,
    long Size,
    string Version,
    GitChangeStatus? Status,
    bool IsBinary,
    bool IsTooLarge,
    string? Content);
