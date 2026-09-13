namespace AgentUp.Server.Features.Commits.DTOs;

using AgentUp.Server.Features.Commits.Models;

public sealed record CommitsStatusResult(
    IReadOnlyList<CommitEntryDto> Entries,
    IReadOnlyList<string> UnassignedFiles,
    CommitsStatusSession? ActiveSession = null,
    GitOperationState? OperationState = null,
    string? QueueWorktreePath = null,
    string? BaseCommit = null,
    string? TipCommit = null,
    long Generation = 0);
