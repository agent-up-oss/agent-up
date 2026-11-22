namespace AgentUp.Server.Features.Files.DTOs;

/// <summary>Replaces an existing text file. <see cref="Version"/> is the version the edit started from.</summary>
public sealed record WorkspaceFileWriteRequest(string? Path, string? Content, string? Version);
