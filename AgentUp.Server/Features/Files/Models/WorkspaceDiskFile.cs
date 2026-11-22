namespace AgentUp.Server.Features.Files.Models;

/// <summary>
/// A file's bytes, or only its size when it is over the content limit. <see cref="Bytes"/> is the
/// whole file whenever it is present, never a prefix, so its version names the real file.
/// </summary>
public sealed record WorkspaceDiskFile(string Path, long Size, byte[]? Bytes);
