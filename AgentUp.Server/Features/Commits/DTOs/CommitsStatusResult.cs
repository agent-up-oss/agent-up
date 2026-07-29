namespace AgentUp.Server.Features.Commits.DTOs;

using AgentUp.Server.Features.Commits.Models;

public sealed record CommitsStatusResult(
    IReadOnlyList<CommitEntryDto> Entries,
    IReadOnlyList<string> UnassignedFiles,
    CommitsStatusSession? ActiveSession = null,
    GitOperationState? OperationState = null);
