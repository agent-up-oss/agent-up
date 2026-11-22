using AgentUp.Server.Features.Files.DTOs;

namespace AgentUp.Server.Features.Files.Models;

/// <summary>A folder entry as the disk reports it, before Git has said anything about it.</summary>
public sealed record WorkspaceDiskEntry(string Name, string Path, WorkspaceFileEntryKind Kind, long? Size);
