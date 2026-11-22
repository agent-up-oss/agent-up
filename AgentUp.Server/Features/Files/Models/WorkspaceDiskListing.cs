namespace AgentUp.Server.Features.Files.Models;

public sealed record WorkspaceDiskListing(string Path, IReadOnlyList<WorkspaceDiskEntry> Entries, bool Truncated);
