namespace AgentUp.Desktop.Features.Git.DTOs;

public sealed record CommitQueueDto(
    IReadOnlyList<CommitQueueEntryDto> Entries,
    IReadOnlyList<string> UnassignedFiles,
    string? QueueWorktreePath,
    string? BaseCommit,
    string? TipCommit,
    long Generation);

public sealed record CommitQueueEntryDto(
    string Slice,
    string Message,
    IReadOnlyList<string> Files,
    string Id,
    string? ParentCommit,
    string? ProposalCommit,
    string State);
