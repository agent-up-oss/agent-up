namespace AgentUp.Server.Features.Commits.DTOs;

public sealed record CommitEntryDto(
    string Slice,
    string Message,
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Tests,
    string Id,
    string PatchId);
