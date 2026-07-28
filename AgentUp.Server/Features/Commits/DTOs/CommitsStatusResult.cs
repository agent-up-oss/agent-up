namespace AgentUp.Server.Features.Commits.DTOs;

public sealed record CommitsStatusResult(
    IReadOnlyList<CommitEntryDto> Entries,
    IReadOnlyList<string> UnassignedFiles,
    CommitsStatusSession? ActiveSession = null);
